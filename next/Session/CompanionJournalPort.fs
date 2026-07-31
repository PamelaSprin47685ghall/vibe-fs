namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Host
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity

type AgentJournalCompanionPort(journal: AgentJournal) =
    let blobWriter = journal.Writer.BlobWriter

    let append (sessionId: SessionId) (providerRun: ProviderRunIdentity option) (fact: AgentFact) =
        match AgentJournal.appendAgent (StreamId.Session sessionId) providerRun fact journal with
        | Ok _ -> Ok()
        | Error failure -> Error(JournalAppendFailure.describe failure)

    let latestBlogText (blog: BlogProjectionState) : Result<BlogText option, string> =
        let rec readFrames frames acc =
            match frames with
            | [] ->
                match List.rev acc with
                | [] -> Ok None
                | values -> Ok(Some(String.concat "\n\n" values))
            | frame :: tail ->
                match blobWriter.Read frame.TextRef with
                | Ok text when HostDigest.sha256Hex text = BlobDigest.value frame.Digest ->
                    readFrames tail (text :: acc)
                | Ok _ -> Error(sprintf "blob digest mismatch: %s" (BlobDigest.value frame.Digest))
                | Error error -> Error error

        readFrames blog.Frames []

    interface ICompanionDurablePort with
        member _.Load(sessionId: SessionId) : Result<CompanionMemory option, string> =
            let projection = AgentJournal.snapshot journal

            projection.AgentProjections.Sessions
            |> Map.tryFind sessionId
            |> function
                | None -> Ok None
                | Some session ->
                    let blog = session.Blog |> Option.defaultValue BlogProjection.empty

                    match latestBlogText blog with
                    | Error error -> Error error
                    | Ok latestB ->
                        Ok(
                            Some
                                { Blog = blog
                                  LatestB = latestB
                                  BloggerSessionId =
                                    session.Companion |> Option.bind (fun companion -> companion.BloggerSessionId) }
                        )

        member _.AppendSuccessful(sessionId, completion) =
            let projection = AgentJournal.snapshot journal

            match Map.tryFind sessionId projection.AgentProjections.Sessions with
            | None -> Error "BlogEntryCommitted requires an existing work session projection"
            | Some session ->
                match session.Companion |> Option.bind (fun companion -> companion.BloggerSessionId) with
                | Some bloggerSessionId when bloggerSessionId = completion.BloggerSessionId ->
                    let blog = session.Blog |> Option.defaultValue BlogProjection.empty

                    match blobWriter.Write completion.Text with
                    | Error error -> Error error
                    | Ok blob ->
                        let fact =
                            AgentFact.BlogEntryCommitted
                                {| SessionId = sessionId
                                   BloggerSessionId = completion.BloggerSessionId
                                   FrameEpochId = blog.FrameEpochId
                                   PreviousIngestTurn = blog.Coverage.IngestCursor.TurnIndex
                                   PreviousIngestPart = blog.Coverage.IngestCursor.PartIndex
                                   NextIngestTurn = completion.NextCursor.TurnIndex
                                   NextIngestPart = completion.NextCursor.PartIndex
                                   PreviousCoverableTurnCutoffExclusive = blog.Coverage.CoverableTurnCutoffExclusive
                                   NextCoverableTurnCutoffExclusive = completion.NextCoverableTurnCutoffExclusive
                                   NextCoveredPrefixDigest = completion.NextCoveredPrefixDigest
                                   TextRef = blob.BlobRef
                                   TextDigest = blob.BlobDigest
                                   ProviderRun = completion.ProviderRun |}

                        match
                            AgentJournal.appendAgent
                                (StreamId.Session sessionId)
                                (Some completion.ProviderRun)
                                fact
                                journal
                        with
                        | Error failure -> Error(JournalAppendFailure.describe failure)
                        | Ok updated ->
                            match Map.tryFind sessionId updated.AgentProjections.Sessions with
                            | Some { Blog = Some blog } -> Ok blog
                            | _ -> Error "BlogEntryCommitted append returned no blog projection"
                | Some _ -> Error "Blogger completion belongs to a different Blogger session"
                | None -> Error "BlogEntryCommitted requires a durably linked Blogger session"

        /// CTX-006 / CTX-012: blob first, then the single BlogSquashCommitted append.
        /// Frame-epoch freshness and the covered-frame bound are checked here so no
        /// call site can commit a squash against a stale or oversized base.
        member _.AppendSquash(sessionId, bloggerSessionId, coveredFrameCount, squashText, providerRun) =
            let projection = AgentJournal.snapshot journal

            match Map.tryFind sessionId projection.AgentProjections.Sessions with
            | None -> Error "BlogSquashCommitted requires an existing work session projection"
            | Some session ->
                match session.Companion |> Option.bind (fun companion -> companion.BloggerSessionId) with
                | Some linked when linked = bloggerSessionId ->
                    let blog = session.Blog |> Option.defaultValue BlogProjection.empty

                    if coveredFrameCount < 1 || coveredFrameCount > List.length blog.Frames then
                        Error(
                            sprintf
                                "BlogSquashCommitted covers %d frames but %d exist"
                                coveredFrameCount
                                (List.length blog.Frames)
                        )
                    else
                        match blobWriter.Write squashText with
                        | Error error -> Error error
                        | Ok blob ->
                            let fact =
                                AgentFact.BlogSquashCommitted
                                    {| SessionId = sessionId
                                       BloggerSessionId = bloggerSessionId
                                       PreviousFrameEpochId = blog.FrameEpochId
                                       NextFrameEpochId = FrameEpochId.next blog.FrameEpochId
                                       CoveredFrameCount = coveredFrameCount
                                       TextRef = blob.BlobRef
                                       TextDigest = blob.BlobDigest
                                       ProviderRun = providerRun |}

                            match
                                AgentJournal.appendAgent (StreamId.Session sessionId) (Some providerRun) fact journal
                            with
                            | Error failure -> Error(JournalAppendFailure.describe failure)
                            | Ok updated ->
                                match Map.tryFind sessionId updated.AgentProjections.Sessions with
                                | Some { Blog = Some blog } -> Ok blog
                                | _ -> Error "BlogSquashCommitted append returned no blog projection"
                | Some _ -> Error "Squash completion belongs to a different Blogger session"
                | None -> Error "BlogSquashCommitted requires a durably linked Blogger session"

        member _.LinkBlogger(sessionId, bloggerSessionId, bloggerAgent) =
            append
                sessionId
                None
                (AgentFact.CompanionBloggerLinked
                    {| SessionId = sessionId
                       BloggerSessionId = bloggerSessionId
                       BloggerAgent = bloggerAgent |})

        member _.CloseBlogger(sessionId) =
            append sessionId None (AgentFact.CompanionBloggerClosed {| SessionId = sessionId |})
