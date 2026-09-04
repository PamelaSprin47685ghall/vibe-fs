namespace Wanxiangshu.Context.Companion.Blogger.Runtime

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// VERIFY-005 single writer for BloggerRequestAbandoned (protocol fail + send-fail + crash-A).
/// Coordinator / EnforcerHost / BloggerCrashRecovery call here; they do not construct the fact.
module BloggerAbandon =

    /// Abandon by explicit RequestId (crash-window A / supersede known open).
    let byRequestId
        (journal: AgentJournal)
        (requestId: BloggerRequestId)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (reason: string)
        : Task =
        task {
            let fact =
                ContextFact.BloggerRequestAbandoned
                    {| RequestId = requestId
                       MainSessionId = mainSessionId
                       BloggerSessionId = bloggerSessionId
                       Reason = reason |}

            let! _ = AgentJournal.appendAgent (StreamId.Session mainSessionId) None fact journal
            return ()
        }

    /// Prefer typed context RequestId; else abandon the open materialization for this Blogger.
    let openRequest
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (preferred: BloggerRequestContext option)
        (reason: string)
        : Task =
        let requestId =
            match preferred with
            | Some ctx -> Some(BloggerRequestContext.requestId ctx)
            | None ->
                (AgentJournal.snapshot journal).AgentProjections.Sessions
                |> Map.tryFind mainSessionId
                |> Option.bind (fun session -> session.BloggerCycles)
                |> Option.bind (fun cycles -> BloggerCycleProjection.tryOpenByBlogger bloggerSessionId cycles)
                |> Option.map (fun openReq -> openReq.RequestId)

        match requestId with
        | None -> Task.FromResult(()) :> Task
        | Some rid -> byRequestId journal rid mainSessionId bloggerSessionId reason
