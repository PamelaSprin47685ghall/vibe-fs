namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Mission.Planning
open Wanxiangshu.OpenCode.Host
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Persistence.Journal

module PluginRecoveryWiring =

    let private isFlightActive
        (bloggerHost: IBloggerRuntimeHost)
        (bloggerSessionId: SessionId)
        (requestId: BloggerRequestId)
        : bool =
        match bloggerHost.TryGetFlight(SessionId.value bloggerSessionId) with
        | Some flight -> BloggerRequestContext.requestId flight = requestId
        | None -> false

    let private seedDevOpsTarget roadSessionId childSessionId target =
        try
            ModelRouting.seedBoundDevOpsModel childSessionId target
        with ex ->
            invalidOp (
                sprintf
                    "execution-model-routing-019: seeding the fixed DevOps target failed (road session %s, devops session %s, target '%s'): %s"
                    (SessionId.value roadSessionId)
                    (SessionId.value childSessionId)
                    target
                    ex.Message
            )

    /// Seed every road's fixed DevOps target into the process-shared routing
    /// table from the durable road projection (execution-model-routing-019).
    ///
    /// The authority chain is durable end to end: the road projection holds the
    /// target string, the road session's handle projection resolves the DevOps
    /// child session, and ModelRouting owns the binding. A road without a bound
    /// target is skipped; a malformed one is refused by the parser rather than
    /// replaced by the current scheduler preference.
    let private seedBoundDevOpsModelTargets (journal: AgentJournal) (workspace: string option) =
        let snapshot = AgentJournal.snapshot journal

        let gitCommonDir =
            workspace
            |> Option.map Wanxiangshu.Persistence.Journal.RuntimePath.gitCommonDir
            |> Option.defaultValue ""
        for KeyValue(sessionId, projection) in snapshot.AgentProjections.Sessions do
            let workId = SessionId.value sessionId
            let _recovery = Wanxiangshu.Mission.Lifecycle.DurableWorkHost.recover gitCommonDir workId
            let target =
                Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected gitCommonDir workId
                |> Option.bind (fun work -> work.DevOpsId)
                |> Option.filter (fun text -> not (String.IsNullOrWhiteSpace text))

            let child =
                projection.Handles
                |> Option.bind (HandleProjection.tryFindByByname "devops")
                |> Option.map (fun handle -> handle.ChildSessionId)

            Option.map2 (seedDevOpsTarget sessionId) child target |> ignore

    let private readExistingFile (fullPath: string) : string option =
        if System.IO.File.Exists fullPath then
            Some(System.IO.File.ReadAllText fullPath)
        else
            None

    let private tryReadPlanFile (root: string) (relPath: string) : string option =
        try
            readExistingFile (System.IO.Path.Combine(root, relPath))
        with _ ->
            None

    /// Algorithm F: recovery reads the unified fold, not the retired Plan events.
    let private emitUnifiedRecoveryEffect (work: Wanxiangshu.Mission.Lifecycle.LifecycleWork) : unit =
        match Wanxiangshu.Mission.Lifecycle.LifecycleFold.recover work with
        | Error reason ->
            Diagnostic.emit "plan-recovery-conflict" [ "work_id", work.WorkId; "reason", reason ]
        | Ok(position, _) ->
            match position with
            | Wanxiangshu.Mission.Lifecycle.RecoveryPosition.DeliveredIdempotent ->
                Diagnostic.emit "plan-work-delivered-idempotent" [ "work_id", work.WorkId ]
            | Wanxiangshu.Mission.Lifecycle.RecoveryPosition.ActiveRebound ->
                Diagnostic.emit "plan-work-active-rebound" [ "work_id", work.WorkId ]
            | Wanxiangshu.Mission.Lifecycle.RecoveryPosition.Conflict ->
                Diagnostic.emit "plan-recovery-conflict" [ "work_id", work.WorkId ]
            | _ -> ()

    let private evaluatePlanRecovery
        (worksOpt: Wanxiangshu.Mission.Lifecycle.LifecycleWork list option)
        (workspaceDirOpt: string option)
        : unit =
        match worksOpt, workspaceDirOpt with
        | Some works, Some _ -> works |> List.iter emitUnifiedRecoveryEffect
        | _ -> ()

    // Stopped. This function has a workspace and no session list that is independent
    // of the lifecycle journal. The public journal cannot list work ids. Missing
    // identity: the session id that would name the work. Do not call tryWorksOfStore.
    let private tryResolveViews
        (_workspace: string)
        : Wanxiangshu.Mission.Lifecycle.LifecycleWork list option =
        None

    let private tryResolveWorkspaceViews
        (workspaceDirOpt: string option)
        : Wanxiangshu.Mission.Lifecycle.LifecycleWork list option =
        match workspaceDirOpt with
        | Some workspace when not (String.IsNullOrWhiteSpace workspace) -> tryResolveViews workspace
        | _ -> None

    let attach (boot: PluginBoot.Boot) : unit =
        let scope = boot.Scope

        // Restart dropped the process-local execution bindings; the durable
        // handle records are the evidence for which parented children a road
        // still owns, so its fixed DevOps can be dispatched to again
        // (crash-reconciliation-020).
        match boot.Journal with
        | Some journal -> SessionBindingRecovery.install journal
        | None -> ()

        scope.AttachDurabilityActivation(fun () ->
            scope.RunBackground(fun () ->
                task {
                    // crash-reconciliation-020: settle the child work runs the
                    // previous runtime left active, so the next handoff to that
                    // child is a fresh root instead of a refused identity.
                    // provider-attempt-recovery-024: the abandoned stale requests
                    // owed one more settlement — their same-source accepted
                    // executions never reached a provider. Decide them before the
                    // runtime reload signal; one failed session must not block the
                    // others, and a boot-time release is an idempotent request.
                    let settleStaleBloggerSession (bloggerSessionId: SessionId) : Task =
                        task {
                            try
                                do!
                                    scope.SignalChatRecovery(
                                        ChatExecutionRecoveryLifecycleEvent.BloggerStaleRequestAbandoned
                                            bloggerSessionId
                                    )
                            with error ->
                                Diagnostic.emit
                                    "stale-blogger-execution-settlement-failed"
                                    [ "blogger_session_id", SessionId.value bloggerSessionId
                                      "error", error.Message ]
                        }

                    // ... and the Blog materializations it left open. No live
                    // execution can own one, and while it stays open the
                    // coordinator never materializes a fresh request — the
                    // Blogger would never ingest the raw tail again
                    // (crash-reconciliation-020 / context-compression-024).
                    let settleAbandonedStaleBloggerSessions (journal: AgentJournal) =
                        task {
                            let bloggerHost = scope.BloggerRuntimeHost
                            let liveFlight = isFlightActive bloggerHost

                            let! staleBloggerSessions = BloggerAbandon.settleStaleOpenAtLoad liveFlight journal

                            for bloggerSessionId in staleBloggerSessions do
                                do! settleStaleBloggerSession bloggerSessionId
                        }

                    match boot.Journal with
                    | Some journal ->
                        do! ChildWorkRecovery.settleOrphanedChildRuns journal
                        do! settleAbandonedStaleBloggerSessions journal
                    | None -> ()

                    do! scope.SignalChatRecovery(ChatExecutionRecoveryLifecycleEvent.PluginRuntimeReloaded)

                    do! scope.SignalChatRecovery(ChatExecutionRecoveryLifecycleEvent.CapacityProjectionReplayed)

                    // execution-model-routing-019: reseed each road's fixed DevOps
                    // model target from the durable road projection. Restart dropped
                    // the process-local binding; without this the first Normal
                    // admission would take the target from the current scheduler
                    // preference and silently overwrite the road's fixed one.
                    match boot.Journal with
                    | Some journal -> seedBoundDevOpsModelTargets journal boot.WorkspaceDirectory
                    | None -> ()

                    // planning-018 / Algorithm J: Plan crash recovery idempotent evaluation
                    // Recover Plan position from durable projection without text guessing.
                    // Delivered remains final; Active rebinds without recreating work or resuming uninvited DevOps.
                    try
                        let viewsOpt = tryResolveWorkspaceViews boot.WorkspaceDirectory
                        evaluatePlanRecovery viewsOpt boot.WorkspaceDirectory
                    with ex ->
                        Diagnostic.emit "plan-crash-recovery-failed" [ "error", ex.Message ]

                    // crash-reconciliation-018: the load-phase normalization above owes
                    // one restart status guidance to the next real user instruction.
                    scope.MarkRestartGuidancePending()
                }))
