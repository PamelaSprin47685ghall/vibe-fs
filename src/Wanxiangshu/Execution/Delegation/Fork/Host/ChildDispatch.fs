namespace Wanxiangshu.Execution.Delegation.Fork.Host

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

open System
open System.Collections.Generic
open System.Threading.Tasks
open Microsoft.FSharp.Control
open Wanxiangshu.OpenCode
open Wanxiangshu.Process
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Persistence.Journal
open FsToolkit.ErrorHandling
open Wanxiangshu.Mission.Obligation.Todo

/// Existing-child dispatch + parent teardown helpers.
module HostForkChildDispatch =
    let private mergeAbortError (errOpt: string option) (abortResult: Result<unit, string>) =
        match errOpt, abortResult with
        | None, Error err -> Some err
        | _ -> errOpt

    let private abortSessionResult (sessions: ISessionHostPort) (childId: SessionId) =
        task {
            try
                return! sessions.AbortSession childId
            with ex ->
                return Error ex.Message
        }

    let private abortOne (sessions: ISessionHostPort) (childId: SessionId) (errOpt: string option) =
        task {
            let! abortResult = abortSessionResult sessions childId
            return mergeAbortError errOpt abortResult
        }

    let private isActiveOwnedHandle (childId: SessionId) (record: HandleRecord) =
        record.ChildSessionId = childId
        && match record.Lifecycle with
           | HandleLifecycle.Active -> true
           | HandleLifecycle.CompletedAwaitingJoin _
           | HandleLifecycle.Abandoned _
           | HandleLifecycle.Retired -> false

    let private isProcessOwnedActiveHandle (handles: AgentLinkageProjection) (agentId: string, childId: SessionId) =
        match HandleProjection.tryFind (HandleController.agentHandle agentId) handles with
        | Some record -> isActiveOwnedHandle childId record
        | None -> false

    let private requireOk (context: string) (result: Result<unit, string>) =
        match result with
        | Ok() -> ()
        | Error err -> raise (InvalidOperationException(sprintf "%s: %s" context err))

    let private awaitUnit (work: Task) : Task<unit> =
        task {
            do! work
            return ()
        }

    let private settlePendingAbandoned
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (settleAbandoned: PendingHostRun -> unit)
        =
        let pending = lock gate (fun () -> pendingRuns.Values |> Seq.toList)

        for run in pending do
            settleAbandoned run

    let private clearChildrenAndRuns
        (gate: obj)
        (children: Dictionary<string, SessionId>)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        =
        lock gate (fun () ->
            children.Clear()
            pendingRuns.Clear())

    let private decideExistingSendAcceptance (sent: Result<unit, string>) (result: ForkResult) =
        match sent, result with
        | Ok(), (ForkResult.Nudged _ | ForkResult.Created _) -> Ok result
        | Ok(), _ -> Error "Existing agent did not accept a new run"
        | Error err, _ -> Error err

    let private nudgeBusyChild
        (sendBusyNudge: string -> SessionId -> Role -> string -> string -> Task<Result<unit, string>>)
        (agentId: string)
        (childId: SessionId)
        (role: Role)
        (agent: string)
        (prompt: string)
        : Task<Result<ForkResult, string>> =
        taskResult {
            do! sendBusyNudge agentId childId role agent prompt
            return ForkResult.Nudged agentId
        }

    let private completeIdleExistingSend
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (sessions: ISessionHostPort)
        (sendChildPrompt: string -> SessionId -> Role -> string -> string -> Task<Result<unit, string>>)
        (agentId: string)
        (childId: SessionId)
        (role: Role)
        (agent: string)
        (prompt: string)
        (enrichedPrompt: string option)
        (run: PendingHostRun)
        (result: ForkResult)
        : Task<Result<ForkResult, string>> =
        taskResult {
            HostForkRunLifecycle.markReady gate pendingRuns journal parentId sessions run None
            let payload = Option.defaultValue prompt enrichedPrompt
            // ofTask keeps Result intact so Error still settles failRun (not bare bind).
            let! sent = sendChildPrompt agentId childId role agent payload |> TaskResultCE.ofTask

            match decideExistingSendAcceptance sent result with
            | Ok accepted -> return accepted
            | Error err ->
                do!
                    HostForkRunLifecycle.failRun gate pendingRuns journal parentId sessions run err
                    |> awaitUnit
                    |> TaskResultCE.ofTask

                return! Error err
        }

    let private dispatchIdleExistingChild
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (sessions: ISessionHostPort)
        (childWorkRecordForRun:
            SessionId -> MagicTodoLwr.BoundedRange -> ProviderRunIdentity -> Task<string option>)
        (xTraceHead: SessionId -> int64)
        (trackOwnedWork: (unit -> Task) -> unit)
        (runtime: ForkRuntime)
        (sendChildPrompt: string -> SessionId -> Role -> string -> string -> Task<Result<unit, string>>)
        (onRunStarted: SessionId -> Role -> unit)
        (agentId: string)
        (childId: SessionId)
        (role: Role)
        (prompt: string)
        (agent: string)
        (enrichedPrompt: string option)
        : Task<Result<ForkResult, string>> =
        taskResult {
            // Idle existing child: new AgentOwnerRoot work via ordinary send.
            //
            // A first-prompt fork (the `enrichedPrompt` Some case) carries the
            // same ARCH-010 payload a brand-new child would receive. The fork
            // boundary must produce one shape for "the child's round
            // assignment" whether the session is fresh or restored from the
            // journal — measured: a post-restart review fork that sent the raw
            // opening prompt broke every canary declaration anchored on the
            // envelope, while a busy nudge (a continuation) must stay raw.
            let run =
                HostForkRunLifecycle.installRun
                    gate
                    pendingRuns
                    journal
                    parentId
                    sessions
                    childWorkRecordForRun
                    xTraceHead
                    trackOwnedWork
                    agentId
                    childId
                    role

            onRunStarted childId role

            let result =
                runtime.Fork(agentId, role, agent, runWork = (fun () -> run.Source.Task))

            match result with
            | ForkResult.NotFound _ ->
                do!
                    HostForkRunLifecycle.failRun
                        gate
                        pendingRuns
                        journal
                        parentId
                        sessions
                        run
                        "Fork runtime is cancelled"
                    |> awaitUnit
                    |> TaskResultCE.ofTask

                return! Error "Fork runtime is cancelled"
            | _ ->
                return!
                    completeIdleExistingSend
                        gate
                        pendingRuns
                        journal
                        parentId
                        sessions
                        sendChildPrompt
                        agentId
                        childId
                        role
                        agent
                        prompt
                        enrichedPrompt
                        run
                        result
        }

    /// Sends a prompt to an already-linked child: if a run is active for this
    /// agent, nudge (fire-and-forget send, carrying role explicitly — after a
    /// host restart OpenCode would otherwise resolve an agent-less child prompt
    /// to the default build agent, not the session's original role); otherwise
    /// install a fresh run and fork it. Shared by HostForkRuntime.Fork's
    /// existing-child path and Reuse, which differ only in how they obtain
    /// `role` before reaching this point.
    let sendToExistingChild
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (sessions: ISessionHostPort)
        (childWorkRecordForRun:
            SessionId -> MagicTodoLwr.BoundedRange -> ProviderRunIdentity -> Task<string option>)
        (xTraceHead: SessionId -> int64)
        (trackOwnedWork: (unit -> Task) -> unit)
        (runtime: ForkRuntime)
        (sendChildPrompt: string -> SessionId -> Role -> string -> string -> Task<Result<unit, string>>)
        (sendBusyNudge: string -> SessionId -> Role -> string -> string -> Task<Result<unit, string>>)
        (onRunStarted: SessionId -> Role -> unit)
        (agentId: string)
        (childId: SessionId)
        (role: Role)
        (prompt: string)
        (agent: string)
        (enrichedPrompt: string option)
        : Task<Result<ForkResult, string>> =
        taskResult {
            let activeRun =
                lock gate (fun () ->
                    match pendingRuns.TryGetValue agentId with
                    | true, run -> Some run
                    | false, _ -> None)

            match activeRun, runtime.IsCancelled with
            | Some _, true -> return! Error "Fork runtime is cancelled"
            | Some _, false ->
                // Active run: BusyAgentNudge continuation (same LogicalRun).
                return! nudgeBusyChild sendBusyNudge agentId childId role agent prompt
            | None, _ ->
                return!
                    dispatchIdleExistingChild
                        gate
                        pendingRuns
                        journal
                        parentId
                        sessions
                        childWorkRecordForRun
                        xTraceHead
                        trackOwnedWork
                        runtime
                        sendChildPrompt
                        onRunStarted
                        agentId
                        childId
                        role
                        prompt
                        agent
                        enrichedPrompt
        }

    /// Abort linked child sessions. Handle retirement has already been written
    /// synchronously by the caller before the async cleanup begins.
    let teardownChildren (sessions: ISessionHostPort) (childIds: SessionId list) : Task<Result<unit, string>> =
        let rec loop remaining firstError =
            task {
                match remaining, firstError with
                | [], Some err -> return Error err
                | [], None -> return Ok()
                | childId :: rest, errOpt ->
                    let! next = abortOne sessions childId errOpt
                    return! loop rest next
            }

        loop childIds None

    /// Cancel parent: fail pending runs, abandon child handles, clear maps.
    ///
    /// `cancelSignals` is invoked with parentId :: childIds so the signal router
    /// ignores further idle/retry events for the torn-down sessions. Unregistering
    /// the routing is the whole cancellation: FALLBACK-003 leaves the cursor
    /// advance to the reconciled snapshot, so a torn-down session simply stops
    /// producing turns to reconcile.
    ///
    /// Side effects that must be visible before the call returns (ForkRuntime
    /// cancellation, signal unrouting, handle retirement) run synchronously before
    /// the async block starts.
    let cancelParent
        (cancelSignals: SessionId seq -> unit)
        (awaitRecovery: unit -> Task<unit>)
        (runtime: ForkRuntime)
        (ptyPort: PtyPort)
        (parentKey: string)
        (parentAbortToken: int)
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (children: Dictionary<string, SessionId>)
        (sessions: ISessionHostPort)
        (journal: AgentJournal option)
        (durableHandles: AgentLinkageProjection option)
        (parentId: SessionId)
        (settleAbandoned: PendingHostRun -> unit)
        (abandonedAt: DateTimeOffset)
        : Task<unit> =
        // Synchronous: make sure observers (runtime.Join, tests, parent abort
        // callbacks) see cancellation immediately.
        runtime.Cancel()

        // Teardown ownership is process-local. Durable Active handles from a
        // previous process are broken historical tools, not resources this
        // runtime may abandon/abort merely because the same parent runtime exists.
        // Explicit /continue discoveries remain dormant outside `children` until
        // a new reuse charge activates them.
        let processOwned =
            lock gate (fun () -> children |> Seq.map (fun kv -> kv.Key, kv.Value) |> Seq.toList)

        let owned =
            match durableHandles with
            | None -> processOwned
            | Some handles -> processOwned |> List.filter (isProcessOwnedActiveHandle handles)

        let childIds = owned |> List.map snd |> List.distinct
        cancelSignals (parentId :: childIds)

        // EXEC-009: durable abandon before aborting. A crash mid-Cancel must not
        // leave a session aborted but still Active/joinable. A leaked abort is
        // recoverable; a leaked live handle is not.
        task {
            let! cancelResult = HandleController.cancelChildren journal parentId (owned |> List.map fst) abandonedAt

            requireOk "Parent handle abandon failed" cancelResult

            do! ptyPort.CloseAll()
            Pty.unregisterParentAbort parentKey parentAbortToken
            settlePendingAbandoned gate pendingRuns settleAbandoned
            do! awaitRecovery ()

            let! teardown = teardownChildren sessions (childIds |> List.distinct)
            requireOk "Parent teardown failed" teardown
            clearChildrenAndRuns gate children pendingRuns
        }
