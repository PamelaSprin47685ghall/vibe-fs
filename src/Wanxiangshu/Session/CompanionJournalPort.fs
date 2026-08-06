namespace Wanxiangshu.Session

open System
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

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
                                  EffectiveFrames = latestB
                                  BloggerSessionId =
                                    session.Companion |> Option.bind (fun companion -> companion.BloggerSessionId)
                                  XTrace = session.XTrace |> Option.defaultValue XTraceProjection.empty }
                        )

        member _.LinkBlogger(sessionId, bloggerSessionId, bloggerAgent) =
            append
                sessionId
                None
                (CompanionFact.CompanionBloggerLinked
                    {| SessionId = sessionId
                       BloggerSessionId = bloggerSessionId
                       BloggerAgent = bloggerAgent |})

        member _.CloseBlogger(sessionId) =
            append sessionId None (CompanionFact.CompanionBloggerClosed {| SessionId = sessionId |})
