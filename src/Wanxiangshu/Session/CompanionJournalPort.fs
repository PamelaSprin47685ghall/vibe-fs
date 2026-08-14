namespace Wanxiangshu.Session

open System
open System.Threading.Tasks
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
        task {
            match! AgentJournal.appendAgent (StreamId.Session sessionId) providerRun fact journal with
            | Ok _ -> return Ok()
            | Error failure -> return Error(JournalAppendFailure.describe failure)
        }

    let latestBlogText (blog: BlogProjectionState) : Task<Result<BlogText option, string>> =
        let rec readFrames frames acc =
            task {
                match frames with
                | [] ->
                    match List.rev acc with
                    | [] -> return Ok None
                    | values -> return Ok(Some(String.concat "\n\n" values))
                | frame :: tail ->
                    match! blobWriter.Read frame.TextRef with
                    | Ok text when HostDigest.sha256Hex text = BlobDigest.value frame.Digest ->
                        return! readFrames tail (text :: acc)
                    | Ok _ -> return Error(sprintf "blob digest mismatch: %s" (BlobDigest.value frame.Digest))
                    | Error error -> return Error error
            }

        readFrames (BlogProjection.frames blog) []

    interface ICompanionDurablePort with
        member _.Load(sessionId: SessionId) : Task<Result<CompanionMemory option, string>> =
            task {
                let projection = AgentJournal.snapshot journal

                match Map.tryFind sessionId projection.AgentProjections.Sessions with
                | None -> return Ok None
                | Some session ->
                    let blog = session.Blog |> Option.defaultValue BlogProjection.empty

                    match! latestBlogText blog with
                    | Error error -> return Error error
                    | Ok latestB ->
                        return
                            Ok(
                                Some
                                    { Blog = blog
                                      EffectiveFrames = latestB
                                      BloggerSessionId =
                                        session.Companion |> Option.bind (fun companion -> companion.BloggerSessionId)
                                      XTrace = session.XTrace |> Option.defaultValue XTraceProjection.empty }
                            )
            }

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
