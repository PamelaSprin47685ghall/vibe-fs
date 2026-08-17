namespace Wanxiangshu.Context.Companion

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Host
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Trace
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

type AgentJournalCompanionPort(journal: AgentJournal) =
    let blobWriter = journal.Writer.BlobWriter

    let append (sessionId: SessionId) (providerRun: ProviderRunIdentity option) (fact: AgentFact) =
        task {
            match! AgentJournal.appendAgent (StreamId.Session sessionId) providerRun fact journal with
            | Ok _ -> return Ok()
            | Error failure -> return Error(JournalAppendFailure.describe failure)
        }

    let latestBlogText (blog: BlogProjectionState) : Task<Result<BlogText option, string>> =
        let finishFrames acc : Result<BlogText option, string> =
            match List.rev acc with
            | [] -> Ok None
            | values -> Ok(Some(String.concat "\n\n" values))

        let rec readFrames (frames: BlogFrame list) acc =
            task {
                match frames with
                | [] -> return finishFrames acc
                | frame :: tail -> return! readFrame frame tail acc
            }

        and readFrame (frame: BlogFrame) tail acc =
            task {
                match! blobWriter.Read frame.TextRef with
                | Ok text when HostDigest.sha256Hex text = BlobDigest.value frame.Digest ->
                    return! readFrames tail (text :: acc)
                | Ok _ -> return Error(sprintf "blob digest mismatch: %s" (BlobDigest.value frame.Digest))
                | Error error -> return Error error
            }

        readFrames (BlogProjection.frames blog) []

    let loadSession (session: SessionAgentProjection) : Task<Result<CompanionMemory option, string>> =
        task {
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

    interface ICompanionDurablePort with
        member _.Load(sessionId: SessionId) : Task<Result<CompanionMemory option, string>> =
            task {
                let projection = AgentJournal.snapshot journal

                match Map.tryFind sessionId projection.AgentProjections.Sessions with
                | None -> return Ok None
                | Some session -> return! loadSession session
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
