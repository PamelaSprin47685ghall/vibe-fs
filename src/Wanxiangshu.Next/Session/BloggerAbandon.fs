namespace Wanxiangshu.Next.Session

open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity

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
        : unit =
        let fact =
            AgentFact.BloggerRequestAbandoned
                {| RequestId = requestId
                   MainSessionId = mainSessionId
                   BloggerSessionId = bloggerSessionId
                   Reason = reason |}

        AgentJournal.appendAgent (StreamId.Session mainSessionId) None fact journal
        |> ignore

    /// Prefer typed context RequestId; else abandon the open materialization for this Blogger.
    let openRequest
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (preferred: BloggerRequestContext option)
        (reason: string)
        : unit =
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
        | None -> ()
        | Some rid -> byRequestId journal rid mainSessionId bloggerSessionId reason
