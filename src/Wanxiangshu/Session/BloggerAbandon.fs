namespace Wanxiangshu.Session

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

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
