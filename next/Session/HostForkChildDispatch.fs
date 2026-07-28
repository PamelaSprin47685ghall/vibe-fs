namespace Wanxiangshu.Next.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Microsoft.FSharp.Control
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

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
        (sessions: ISessionHostPort)
        (runtime: ForkRuntime)
        (sendChildPrompt: string -> SessionId -> AgentRole -> string -> string -> Task<Result<unit, string>>)
        (sendBusyNudge: string -> SessionId -> AgentRole -> string -> string -> Task<Result<unit, string>>)
        (onRunStarted: SessionId -> AgentRole -> unit)
        (agentId: string)
        (childId: SessionId)
        (role: AgentRole)
        (prompt: string)
        (agent: string)
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
                let run =
                    HostForkRunLifecycle.installRun gate pendingRuns sessions agentId childId role

                onRunStarted childId role

                let result =
                    runtime.Fork(agentId, role, runWork = (fun () -> run.Source.Task), agent = agent)

                match result with
                | ForkResult.NotFound _ ->
                    HostForkRunLifecycle.failRun gate pendingRuns sessions run "Fork runtime is cancelled"
                    return Error "Fork runtime is cancelled"
                | _ ->
                    HostForkRunLifecycle.markReady gate run
                    let! sent = sendChildPrompt agentId childId role agent prompt

                    match sent, result with
                    | Ok(), ForkResult.Nudged _ -> return Ok result
                    | Ok(), _ ->
                        HostForkRunLifecycle.failRun
                            gate
                            pendingRuns
                            sessions
                            run
                            "Existing agent did not accept a new run"

                        return Error "Existing agent did not accept a new run"
                    | Error err, _ ->
                        HostForkRunLifecycle.failRun gate pendingRuns sessions run err
                        return Error err
        }

    /// Persist AgentUnlinked facts for each distinct child BEFORE aborting, so a
    /// crash mid-Cancel cannot leave a session aborted but still linked (which
    /// would make a restart restore a dead child). A leaked abort is recoverable;
    /// a leaked link is not.
    ///
    /// Timing adjudication: unlink is driven ONLY by the parent's Cancel (the sole
    /// teardown path — HostForkRuntime has no other Dispose hook). There is no
    /// child-normal-close host event (host docs confirm no durable child-close event), so
    /// a child that completes normally intentionally KEEPS its link: the child
    /// stays addressable for Reuse/nudge.
    ///
    let unlinkChildren
        (journal: AgentJournal option)
        (parentId: SessionId)
        (childIds: SessionId list)
        : Result<unit, string> =
        match journal with
        | None -> Ok()
        | Some journal ->
            let rec appendRemaining ids =
                match ids with
                | [] -> Ok()
                | childId :: rest ->
                    match
                        AgentJournal.appendAgent
                            (StreamId.Session parentId)
                            None
                            (AgentFact.AgentUnlinked
                                {| ParentId = parentId
                                   ChildId = ChildId.create (SessionId.value childId) |})
                            journal
                    with
                    | Ok _ -> appendRemaining rest
                    | Error failure -> Error(sprintf "%A" failure.Failure)

            appendRemaining childIds

    /// Abort linked child sessions.  Unlink facts have already been written
    /// synchronously by the caller before the async cleanup begins.
    let teardownChildren
        (sessions: ISessionHostPort)
        (parentId: SessionId)
        (children: Dictionary<string, SessionId>)
        (gate: obj)
        : Task<Result<unit, string>> =
        task {
            let childIds = lock gate (fun () -> children.Values |> Seq.distinct |> Seq.toList)
            let mutable firstError: string option = None

            for childId in childIds do
                try
                    let! abortResult = sessions.AbortSession childId

                    match abortResult, firstError with
                    | Error err, None -> firstError <- Some err
                    | _ -> ()
                with ex ->
                    if firstError.IsNone then
                        firstError <- Some ex.Message

            match firstError with
            | Some err -> return Error err
            | None -> return Ok()
        }

    /// Cancel parent: fail pending runs, durable-unlink children, clear maps.
    /// `cancelFallback` is invoked with parentId :: childIds to stop pending
    /// ProviderRetryAttempt flushes for the torn-down sessions.
    /// `cancelSignals` is invoked with parentId :: childIds so the signal router
    /// ignores further idle/retry events for the torn-down sessions.
    ///
    /// Side effects that must be visible before the call returns (ForkRuntime
    /// cancellation, fallback flush cancellation, durable unlink) run
    /// synchronously before the async block starts.
    let cancelParent
        (cancelFallback: SessionId seq -> unit)
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
        (parentId: SessionId)
        (complete: PendingHostRun -> TerminalOutcome -> unit)
        : Async<unit> =
        // Synchronous: make sure observers (runtime.Join, tests, parent abort
        // callbacks) see cancellation and fallback flush immediately.
        runtime.Cancel()

        let childIds = lock gate (fun () -> children.Values |> Seq.distinct |> Seq.toList)
        cancelSignals (parentId :: childIds)
        cancelFallback (parentId :: childIds)

        match unlinkChildren journal parentId childIds with
        | Error err ->
            // Journal failure during unlink is a durable-state bug; surface it.
            async { return raise (InvalidOperationException(sprintf "Parent unlink failed: %s" err)) }
        | Ok() ->
            async {
                do! Async.AwaitTask(ptyPort.CloseAll())
                Pty.unregisterParentAbort parentKey parentAbortToken

                let pending = lock gate (fun () -> pendingRuns.Values |> Seq.toList)

                for run in pending do
                    complete run (TerminalOutcome.Failed "cancelled")

                do! Async.AwaitTask(awaitRecovery ())

                let! teardown = Async.AwaitTask(teardownChildren sessions parentId children gate)

                match teardown with
                | Ok() ->
                    lock gate (fun () ->
                        children.Clear()
                        pendingRuns.Clear())

                    return ()
                | Error err -> return raise (InvalidOperationException(sprintf "Parent teardown failed: %s" err))
            }
