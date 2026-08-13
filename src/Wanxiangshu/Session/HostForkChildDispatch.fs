namespace Wanxiangshu.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Microsoft.FSharp.Control
open Wanxiangshu.OpenCode
open Wanxiangshu.Process
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Journal

/// Existing-child dispatch + parent teardown helpers.
module HostForkChildDispatch =
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
        (childWorkRecordFor: SessionId -> Task<string option>)
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
        task {
            let activeRun =
                lock gate (fun () ->
                    match pendingRuns.TryGetValue agentId with
                    | true, run -> Some run
                    | false, _ -> None)

            match activeRun with
            | Some _ when runtime.IsCancelled -> return Error "Fork runtime is cancelled"
            | Some _ ->
                // Active run: BusyAgentNudge continuation (same LogicalRun).
                let! sent = sendBusyNudge agentId childId role agent prompt

                match sent with
                | Ok() -> return Ok(ForkResult.Nudged agentId)
                | Error err -> return Error err
            | None ->
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
                        childWorkRecordFor
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

                    return Error "Fork runtime is cancelled"
                | _ ->
                    HostForkRunLifecycle.markReady gate pendingRuns journal parentId sessions run None

                    let payload = Option.defaultValue prompt enrichedPrompt
                    let! sent = sendChildPrompt agentId childId role agent payload

                    match sent, result with
                    | Ok(), (ForkResult.Nudged _ | ForkResult.Created _) -> return Ok result
                    | Ok(), _ ->
                        do!
                            HostForkRunLifecycle.failRun
                                gate
                                pendingRuns
                                journal
                                parentId
                                sessions
                                run
                                "Existing agent did not accept a new run"

                        return Error "Existing agent did not accept a new run"
                    | Error err, _ ->
                        do! HostForkRunLifecycle.failRun gate pendingRuns journal parentId sessions run err
                        return Error err
        }

    /// Abort linked child sessions. Handle retirement has already been written
    /// synchronously by the caller before the async cleanup begins.
    let teardownChildren (sessions: ISessionHostPort) (childIds: SessionId list) : Task<Result<unit, string>> =
        task {
            let rec loop remaining firstError =
                task {
                    match remaining, firstError with
                    | [], Some err -> return Error err
                    | [], None -> return Ok()
                    | childId :: rest, errOpt ->
                        try
                            let! abortResult = sessions.AbortSession childId

                            match abortResult, errOpt with
                            | Error err, None -> return! loop rest (Some err)
                            | _ -> return! loop rest errOpt
                        with ex ->
                            match errOpt with
                            | None -> return! loop rest (Some ex.Message)
                            | Some _ -> return! loop rest errOpt
                }

            return! loop childIds None
        }

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
        (complete: PendingHostRun -> TerminalOutcome -> unit)
        (abandonedAt: DateTimeOffset)
        : Async<unit> =
        // Synchronous: make sure observers (runtime.Join, tests, parent abort
        // callbacks) see cancellation immediately.
        runtime.Cancel()

        let owned =
            match durableHandles with
            | Some handles ->
                HandleProjection.activeHandles handles
                |> List.choose (fun record ->
                    match HandleId.tryAgent record.Handle with
                    | Some handle -> Some(AgentHandleId.value handle, record.ChildSessionId)
                    | None -> None)
            | None -> lock gate (fun () -> children |> Seq.map (fun kv -> kv.Key, kv.Value) |> Seq.toList)

        let childIds = owned |> List.map snd |> List.distinct
        cancelSignals (parentId :: childIds)

        // EXEC-009: durable abandon before aborting. A crash mid-Cancel must not
        // leave a session aborted but still Active/joinable. A leaked abort is
        // recoverable; a leaked live handle is not.
        async {
            match!
                HandleController.cancelChildren journal parentId (owned |> List.map fst) abandonedAt
                |> Async.AwaitTask
            with
            | Error err ->
                // Journal failure during abandon is a durable-state bug; surface it.
                return raise (InvalidOperationException(sprintf "Parent handle abandon failed: %s" err))
            | Ok() ->
                do! Async.AwaitTask(ptyPort.CloseAll())
                Pty.unregisterParentAbort parentKey parentAbortToken

                let pending = lock gate (fun () -> pendingRuns.Values |> Seq.toList)

                for run in pending do
                    complete run (TerminalOutcome.Failed "cancelled")

                do! Async.AwaitTask(awaitRecovery ())

                let! teardown = Async.AwaitTask(teardownChildren sessions (childIds |> List.distinct))

                match teardown with
                | Ok() ->
                    lock gate (fun () ->
                        children.Clear()
                        pendingRuns.Clear())

                    return ()
                | Error err -> return raise (InvalidOperationException(sprintf "Parent teardown failed: %s" err))
        }
