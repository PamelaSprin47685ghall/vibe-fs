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

            let _recovery =
                Wanxiangshu.Mission.Lifecycle.DurableWorkHost.recover gitCommonDir workId

            let target =
                Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected gitCommonDir workId
                |> Option.bind (fun work -> work.DevOpsId)
                |> Option.filter (fun text -> not (String.IsNullOrWhiteSpace text))

            let child =
                projection.Handles
                |> Option.bind (HandleProjection.tryFindByByname "devops")
                |> Option.map (fun handle -> handle.ChildSessionId)

            Option.map2 (seedDevOpsTarget sessionId) child target |> ignore

    let private firstUserPrompt (messages: SessionMessage list) : string option =
        messages
        |> List.tryFind (fun message -> message.Role = "user")
        |> Option.map (fun message ->
            Wanxiangshu.Interaction.Repair.CompletedTurnClassifier.partsText message.Parts)
        |> Option.filter (fun text -> not (String.IsNullOrEmpty text))

    let private observeBoundDelivery
        (gitCommonDir: string)
        (workId: string)
        (execSessionId: string)
        (snapshots: ISessionSnapshotPort)
        (retirementBlockers: string -> string array)
        : Task =
        task {
            match! snapshots.GetMessages(SessionId.create execSessionId) with
            | Ok messages ->
                match firstUserPrompt messages with
                | None ->
                    Diagnostic.emit
                        "plan-delivery-recovery-pending"
                        [ "work_id", workId
                          "exec_session_id", execSessionId
                          "reason", "bound session has no observed user prompt" ]
                | Some prompt ->
                    match
                        Wanxiangshu.Mission.Planning.PlanTools.reconcileBoundExecution
                            gitCommonDir
                            workId
                            execSessionId
                            prompt
                            (retirementBlockers workId)
                    with
                    | Ok _ ->
                        Diagnostic.emit
                            "plan-delivery-recovery-settled"
                            [ "work_id", workId; "exec_session_id", execSessionId ]
                    | Error reason ->
                        Diagnostic.emit
                            "plan-delivery-recovery-refused"
                            [ "work_id", workId
                              "exec_session_id", execSessionId
                              "reason", reason ]
            | Error reason ->
                Diagnostic.emit
                    "plan-delivery-recovery-observation-failed"
                    [ "work_id", workId
                      "exec_session_id", execSessionId
                      "reason", reason ]
        }

    let private discoverUnboundDelivery
        (gitCommonDir: string)
        (workId: string)
        (operationId: string)
        (snapshots: ISessionSnapshotPort option)
        (discovery: ISessionDiscoveryPort)
        (retirementBlockers: string -> string array)
        : Task =
        task {
            let title = "wanxiangshu-delivery:" + operationId

            match! discovery.FindRootSessionsByTitle title with
            | Error reason ->
                Diagnostic.emit
                    "plan-delivery-recovery-discovery-failed"
                    [ "work_id", workId; "operation_id", operationId; "reason", reason ]
            | Ok [] ->
                Diagnostic.emit
                    "plan-delivery-recovery-unbound"
                    [ "work_id", workId
                      "operation_id", operationId
                      "reason", "no exact Host session is currently observable" ]
            | Ok [ sessionId ] ->
                let execSessionId = SessionId.value sessionId

                match
                    Wanxiangshu.Mission.Planning.PlanTools.bindObservedExecutionSession
                        gitCommonDir
                        workId
                        operationId
                        execSessionId
                with
                | Error reason ->
                    Diagnostic.emit
                        "plan-delivery-recovery-bind-refused"
                        [ "work_id", workId
                          "operation_id", operationId
                          "exec_session_id", execSessionId
                          "reason", reason ]
                | Ok() ->
                    match snapshots with
                    | Some snapshot ->
                        do! observeBoundDelivery gitCommonDir workId execSessionId snapshot retirementBlockers
                    | None ->
                        Diagnostic.emit
                            "plan-delivery-recovery-no-snapshot"
                            [ "work_id", workId; "exec_session_id", execSessionId ]
            | Ok sessions ->
                Diagnostic.emit
                    "plan-delivery-recovery-ambiguous"
                    [ "work_id", workId
                      "operation_id", operationId
                      "matches", string sessions.Length ]
        }

    /// A delivery session is bound durably before its first prompt is sent.
    /// After restart we never resend that prompt: OpenCode 1.18.29 accepts a
    /// caller message id but does not make repeated parts idempotent. Instead
    /// we read the exact bound session and settle only when the real first user
    /// message proves the reserved plan bytes reached that container.
    let private reconcilePendingDeliveries
        (journal: AgentJournal)
        (workspace: string option)
        (snapshotPort: ISessionSnapshotPort option)
        (discoveryPort: ISessionDiscoveryPort option)
        (retirementBlockers: string -> string array)
        : Task =
        task {
            let gitCommonDir =
                workspace
                |> Option.map Wanxiangshu.Persistence.Journal.RuntimePath.gitCommonDir
                |> Option.defaultValue ""

            if String.IsNullOrWhiteSpace gitCommonDir then
                ()
            else
                let sessions =
                    (AgentJournal.snapshot journal).AgentProjections.Sessions
                    |> Seq.map (fun (KeyValue(sessionId, _)) -> SessionId.value sessionId)
                    |> Seq.distinct
                    |> Seq.toArray

                for workId in sessions do
                    match Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected gitCommonDir workId with
                    | Some work when work.PendingDelivery ->
                        match work.PendingExecSessionId, snapshotPort with
                        | Some execSessionId, Some snapshots ->
                            do! observeBoundDelivery gitCommonDir workId execSessionId snapshots retirementBlockers
                        | None, _ ->
                            match work.PendingOperationId, discoveryPort with
                            | Some operationId, Some discovery ->
                                do!
                                    discoverUnboundDelivery
                                        gitCommonDir
                                        workId
                                        operationId
                                        snapshotPort
                                        discovery
                                        retirementBlockers
                            | Some operationId, None ->
                                Diagnostic.emit
                                    "plan-delivery-recovery-no-discovery"
                                    [ "work_id", workId; "operation_id", operationId ]
                            | None, _ ->
                                Diagnostic.emit
                                    "plan-delivery-recovery-unbound"
                                    [ "work_id", workId
                                      "reason", "pending delivery has no operation identity" ]
                        | Some execSessionId, None ->
                            Diagnostic.emit
                                "plan-delivery-recovery-no-snapshot"
                                [ "work_id", workId
                                  "exec_session_id", execSessionId ]
                    | _ -> ()
        }

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

    let attach (boot: PluginBoot.Boot) : unit =
        let scope = boot.Scope
        let snapshotPort = SessionSnapshotPort.create boot.Input
        let discoveryPort = SessionSnapshotPort.createDiscovery boot.Input
        let retirementBlockers sessionId = scope.RetirementBlockersFor sessionId |> List.toArray

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
                        do!
                            reconcilePendingDeliveries
                                journal
                                boot.WorkspaceDirectory
                                snapshotPort
                                discoveryPort
                                retirementBlockers
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

                    // crash-reconciliation-018: the load-phase normalization above owes
                    // one restart status guidance to the next real user instruction.
                    scope.MarkRestartGuidancePending()
                }))
