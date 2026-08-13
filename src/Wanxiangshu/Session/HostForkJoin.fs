namespace Wanxiangshu.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Microsoft.FSharp.Control
open Wanxiangshu.Domain.ChildRecovery
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.OpenCode
open Wanxiangshu.Process
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Journal
open Wanxiangshu.Session.AgentRoleIdentity

/// Wave 3 (Proposal ch. 12): HostForkRuntime keeps the state/resource spine;
/// the join/await workflow lives here as free functions over the runtime's
/// internal accessors. Logic is identical to the former members — only the
/// reference plumbing changed (this.X → function arg, private state → accessors).
module HostForkJoin =

    /// EXEC-009 + EXEC-018 + GREEN-5: agent facts from Journal; PTY from mailbox as PtyJoinItem.
    /// journal=None: agent join fail-closed; pure PTY drain still allowed.
    /// Abandoned items join the same ResultsAvailable batch — never withhold
    /// completed results behind a top-level ForkError.Abandoned.
    /// EXEC-020: PTY stays JoinItem/PtyJoinItem until renderer (no toRunCompletion on batch path).
    let private tryDrainAvailable
        (runtime: HostForkRuntime)
        (maxCount: int)
        : Task<Result<JoinWaitOutcome<JoinItem>, ForkError> option> =
        task {
            let cap = min maxCount JoinBatch.Max

            if cap <= 0 then
                return None
            else
                // Clear agent wakes — they never carry payload (GREEN-5).
                ignore (runtime.Runtime.DrainAgentWakes JoinBatch.Max)

                match runtime.Journal with
                | None ->
                    let hasActiveAgents =
                        runtime.Runtime.ActiveRunCount > 0
                        || lock runtime.Gate (fun () -> runtime.PendingRuns.Count > 0)
                        || runtime.Runtime.PendingCompletionCount > runtime.Runtime.PendingPtyCount

                    if hasActiveAgents then
                        return
                            Some(
                                Error(
                                    ForkError.NotFound
                                        "agent join requires journal; journal=None is fail-closed for agent handles"
                                )
                            )
                    else
                        let ptyBatch =
                            runtime.Runtime.DrainPtyCompletions cap |> List.map JoinItem.ofPtyJoinItem

                        match NonEmptyBatch.tryOfList ptyBatch with
                        | Some batch -> return Some(Ok(ResultsAvailable batch))
                        | None -> return None
                | Some durable ->
                    match! JoinDrain.drainFromJournal durable runtime.ParentId cap (runtime.Clock.UtcNow()) with
                    | Error e -> return Some(Error e)
                    | Ok durableBatch ->
                        let remaining = cap - List.length durableBatch
                        let agentItems = durableBatch |> List.map JoinItem.ofAgentRunCompletion

                        let ptyItems =
                            if remaining <= 0 then
                                []
                            else
                                runtime.Runtime.DrainPtyCompletions remaining |> List.map JoinItem.ofPtyJoinItem

                        match NonEmptyBatch.tryOfList (agentItems @ ptyItems) with
                        | Some batch -> return Some(Ok(ResultsAvailable batch))
                        | None -> return None
        }

    /// Permit gate shared by JoinWithPermit / JoinAvailableWithPermit / AwaitAgentWithPermit.
    let private validatePermit (runtime: HostForkRuntime) (permit: FamilyRecoveryPermit) : Result<unit, ForkError> =
        let root = FamilyRecoveryPermit.root permit
        let permitSeq = FamilyRecoveryPermit.journalSequence permit

        if root <> runtime.ParentId then
            Error(
                ForkError.NotFound(
                    sprintf
                        "family recovery permit root mismatch: permit=%s runtime=%s"
                        (SessionId.value root)
                        (SessionId.value runtime.ParentId)
                )
            )
        else
            match runtime.Journal with
            | None ->
                Error(
                    ForkError.NotFound
                        "family recovery permit requires journal; pure PTY join must not use JoinWithPermit"
                )
            | Some durable ->
                let currentSeq = JournalRevision.value (AgentJournal.revision durable)

                if currentSeq < permitSeq then
                    Error(
                        ForkError.NotFound(
                            sprintf
                                "family recovery permit journalSequence stale: permit=%d current=%d"
                                permitSeq
                                currentSeq
                        )
                    )
                else
                    let current =
                        RecoveryClosureProjection.discover
                            root
                            (AgentJournal.snapshot durable).AgentProjections
                            currentSeq

                    // EXEC-023 monotone admission: the permit proves the family it recovered is
                    // closed, so what invalidates it is a member GOING MISSING — not the family
                    // growing. A child forking a grandchild mid-join changes the closure digest
                    // while invalidating no recovery, and comparing digests refused exactly those
                    // legitimate joins (`temporal-ownership-unhappy-path`, deterministically).
                    match FamilyRecoveryPermit.missingFrom (RecoveryClosure.members current) permit with
                    | [] -> Ok()
                    | missing ->
                        Error(
                            ForkError.NotFound(
                                sprintf
                                    "family recovery permit closure lost members: missing=%s permit=%s current=%s"
                                    (String.concat "," missing)
                                    (FamilyRecoveryPermit.describeClosure permit)
                                    current.Digest
                            )
                        )

    /// EXEC-017 / EXEC-018: bounded batch join with typed local interrupt
    /// (≠ runtime.Cancel). GREEN-5: agent facts from Journal; agent mailbox
    /// channel is wake-only. PTY facts from PTY mailbox channel as PtyJoinItem
    /// (EXEC-015 / EXEC-020). journal=None agent join fails closed.
    let joinAvailable
        (runtime: HostForkRuntime)
        (maxCount: int)
        (interrupt: Task<JoinInterruptReason>)
        : Task<Result<JoinWaitOutcome<JoinItem>, ForkError>> =
        let cap = min (max 0 maxCount) JoinBatch.Max

        let hasWork () =
            runtime.Runtime.ActiveRunCount > 0
            || runtime.Runtime.PendingCompletionCount > 0
            || lock runtime.Gate (fun () -> runtime.PendingRuns.Count > 0 || runtime.PtyRuns.Count > 0)
            || match runtime.Journal with
               | None -> false
               | Some durable ->
                   let p = AgentJournal.handleProjection durable runtime.ParentId

                   not (List.isEmpty (HandleProjection.joinable p))
                   || not (List.isEmpty (HandleProjection.reportableAbandoned p))
                   || not (List.isEmpty (HandleProjection.activeHandles p))

        /// Outer race arms: mailbox wake | journal change | local interrupt.
        /// PulseWake after race so a losing WaitForWake waiter never piles up.
        let rec loop () : Task<Result<JoinWaitOutcome<JoinItem>, ForkError>> =
            task {
                match! tryDrainAvailable runtime cap with
                | Some result -> return result
                | None ->
                    if runtime.Runtime.IsCancelled then
                        return Error ForkError.Cancelled
                    elif not (hasWork ()) then
                        return Error ForkError.NothingToJoin
                    else
                        match runtime.Journal with
                        | Some durable ->
                            let _, fromRev = durable.SnapshotWithRevision

                            match! tryDrainAvailable runtime cap with
                            | Some result -> return result
                            | None ->
                                // Tagged race arms without nested task{} (dsl-ownership).
                                // kind: 0=wake(+reason), 1=journal change, 2=user interrupt.
                                let wakeTask: Task<obj> =
                                    emitJsExpr
                                        (runtime.Runtime.WaitForWake())
                                        "$0.then(function (r) { return { kind: 0, reason: r }; })"

                                let changeTask: Task<obj> =
                                    emitJsExpr
                                        (durable.AwaitChangeFrom fromRev)
                                        "$0.then(function () { return { kind: 1 }; })"

                                let interruptTask: Task<obj> =
                                    emitJsExpr interrupt "$0.then(function (r) { return { kind: 2, reason: r }; })"

                                let! winner =
                                    emitJsExpr (wakeTask, changeTask, interruptTask) "Promise.race([$0, $1, $2])"
                                    : Task<obj>

                                runtime.Runtime.PulseWake()

                                match! tryDrainAvailable runtime cap with
                                | Some result -> return result
                                | None ->
                                    let kind: int = emitJsExpr winner "$0.kind"

                                    if kind = 0 then
                                        let reason: MailboxWakeReason = emitJsExpr winner "$0.reason"

                                        match reason with
                                        | MailboxCancelled -> return Error ForkError.Cancelled
                                        | CompletionMayBeAvailable
                                        | LocalInterrupt _ -> return! loop ()
                                    elif kind = 2 then
                                        let interruptReason: JoinInterruptReason = emitJsExpr winner "$0.reason"
                                        return Ok(Interrupted interruptReason)
                                    else
                                        return! loop ()
                        | None ->
                            let! signal = runtime.Runtime.WaitForSignal interrupt

                            match! tryDrainAvailable runtime cap with
                            | Some result -> return result
                            | None ->
                                match signal with
                                | MailboxCancelled -> return Error ForkError.Cancelled
                                | LocalInterrupt reason -> return Ok(Interrupted reason)
                                | CompletionMayBeAvailable -> return! loop ()
            }

        // A tool turn may contain duplicate join calls. Only one waiter may own the
        // runtime wake channels; a second waiter would otherwise consume no fact and
        // remain parked after the first waiter consumes the sole wake.
        let acquired = runtime.TryAcquireJoin()

        if not acquired then
            Task.FromResult(Error ForkError.JoinInProgress)
        elif cap <= 0 then
            runtime.ReleaseJoin()
            Task.FromResult(Error ForkError.Empty)
        else
            // GREEN-4: JoinAvailable does not start recovery; permit path already recovered.
            task {
                try
                    return! loop ()
                finally
                    runtime.ReleaseJoin()
            }

    /// Compatibility single-result join (Executor map/reduce agent path).
    /// None is unbounded; Some is an explicit internal waiter budget.
    /// Projects JoinItem → RunCompletion for callers that still need agent Outcome.
    let join (runtime: HostForkRuntime) (timeoutMs: int option) : Task<Result<RunCompletion, ForkError>> =
        let budgetMs = timeoutMs

        let headAsRunCompletion (batch: NonEmptyBatch<JoinItem>) : RunCompletion =
            let completedAt = runtime.Clock.UtcNow()

            match NonEmptyBatch.toList batch |> List.head with
            | AgentItem(AgentCompletedItem payload) ->
                { RunId = payload.RunId
                  AgentId = payload.AgentId
                  AgentName = payload.AgentId
                  Role = payload.Role
                  Outcome = AgentCompleted payload
                  CompletedAt = completedAt }
            | AgentItem(AgentFailedItem payload) ->
                { RunId = payload.RunId
                  AgentId = payload.AgentId
                  AgentName = payload.AgentId
                  Role = defaultArg payload.Role Role.Distiller
                  Outcome = AgentFailed payload
                  CompletedAt = completedAt }
            | AgentItem(AgentAbandonedItem(agentId, reason)) ->
                { RunId = "abandoned-" + agentId
                  AgentId = agentId
                  AgentName = agentId
                  Role = Role.Distiller
                  Outcome = AgentAbandoned(agentId, reason)
                  CompletedAt = completedAt }
            | PtyItem item -> PtyJoinItem.toRunCompletion item completedAt

        task {
            let deadlineHandle, interrupt =
                match budgetMs with
                | Some milliseconds ->
                    let handle = runtime.Timers.Delay milliseconds

                    let arm =
                        emitJsExpr handle.Delay "$0.then(function () { return 'DeadlineExpired'; })"

                    Some handle, arm
                | None ->
                    let tcs =
                        TaskCompletionSource<JoinInterruptReason>(TaskCreationOptions.RunContinuationsAsynchronously)

                    None, tcs.Task

            try
                let! outcome = joinAvailable runtime 1 interrupt

                match outcome with
                | Error e -> return Error e
                | Ok(Interrupted _) ->
                    // Deadline interrupt under legacy Join API → TimedOut after final drain.
                    match! tryDrainAvailable runtime 1 with
                    | Some(Ok(ResultsAvailable batch)) -> return Ok(headAsRunCompletion batch)
                    | Some(Error e) -> return Error e
                    | Some(Ok(Interrupted _))
                    | None -> return Error ForkError.TimedOut
                | Ok(ResultsAvailable batch) -> return Ok(headAsRunCompletion batch)
            finally
                deadlineHandle |> Option.iter (fun h -> h.Cancel())
        }

    /// P0-RECOVERY-JOIN-001 + GREEN-4: permit-gated single-result join.
    /// Validates root / journalSequence lower bound / closureDigest only; never starts recovery.
    let joinWithPermit
        (runtime: HostForkRuntime)
        (permit: FamilyRecoveryPermit)
        (timeoutMs: int option)
        : Task<Result<RunCompletion, ForkError>> =
        match validatePermit runtime permit with
        | Error e -> Task.FromResult(Error e)
        | Ok() -> join runtime timeoutMs

    /// EXEC-018 batch join under FamilyRecoveryPermit.
    /// GREEN-4: validate permit then drain; does not start RestoreHandles.
    /// Batch carries JoinItem so PtyAborted survives to renderer (EXEC-020).
    let joinAvailableWithPermit
        (runtime: HostForkRuntime)
        (permit: FamilyRecoveryPermit)
        (maxCount: int)
        (interrupt: Task<JoinInterruptReason>)
        : Task<Result<JoinWaitOutcome<JoinItem>, ForkError>> =
        match validatePermit runtime permit with
        | Error e -> Task.FromResult(Error e)
        | Ok() -> joinAvailable runtime maxCount interrupt

    let awaitAgent
        (runtime: HostForkRuntime)
        (agentId: string)
        (timeoutMs: int option)
        : Task<Result<RunCompletion, string>> =
        runtime.Runtime.AwaitAgent(agentId, ?timeoutMs = timeoutMs)

    /// Permit-gated targeted agent await. validatePermit then AwaitAgent;
    /// string errors map to ForkError.NotFound. No second RunCompletion truth source.
    let awaitAgentWithPermit
        (runtime: HostForkRuntime)
        (permit: FamilyRecoveryPermit)
        (agentId: string)
        (timeoutMs: int option)
        : Task<Result<RunCompletion, ForkError>> =
        match validatePermit runtime permit with
        | Error e -> Task.FromResult(Error e)
        | Ok() ->
            task {
                match! awaitAgent runtime agentId timeoutMs with
                | Error msg -> return Error(ForkError.NotFound msg)
                | Ok completion -> return Ok completion
            }

    /// Targeted cancel for one forked agent (Executor map/reduce sibling abort).
    /// Completes the pending run cell and aborts the Host child so Join unblocks;
    /// ForkRuntime CTS cancel alone cannot settle Source.Task.
    let cancelAgent (runtime: HostForkRuntime) (agentId: string) : unit =
        runtime.Runtime.CancelAgent(agentId)

        let pending, childId =
            lock runtime.Gate (fun () ->
                let run =
                    match runtime.PendingRuns.TryGetValue agentId with
                    | true, r -> Some r
                    | false, _ -> None

                let child =
                    match runtime.Children.TryGetValue agentId with
                    | true, id -> Some id
                    | false, _ -> None

                run, child)

        match pending with
        | Some run ->
            // IDistillationRuntime.CancelAgent is synchronous; durable completion
            // continues on the returned Task while the physical abort starts below.
            runtime.FailRun(run, "cancelled") |> ignore
        | None -> ()

        match childId with
        | Some id ->
            runtime.Sessions.AbortSession id
            |> Async.AwaitTask
            |> Async.Ignore
            |> Async.StartImmediate
        | None -> ()
