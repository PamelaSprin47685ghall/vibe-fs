namespace Wanxiangshu.Execution.Delegation.Fork.Host

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Microsoft.FSharp.Control
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
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Execution.Delegation.Fork.ChildRecovery
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.OpenCode
open Wanxiangshu.Process
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Persistence.Journal

/// Wave 3 (Proposal ch. 12): HostForkRuntime keeps the state/resource spine;
/// the join/await workflow lives here as free functions over the runtime's
/// internal accessors. Logic is identical to the former members — only the
/// reference plumbing changed (this.X → function arg, private state → accessors).
module HostForkJoin =

    type private JoinPoll =
        | Done of Result<JoinWaitOutcome<JoinItem>, ForkError>
        | Retry

    let private currentProcessHandle (runtime: HostForkRuntime) (record: HandleRecord) =
        match HandleId.tryAgent record.Handle with
        | None -> false
        | Some handleId -> runtime.OwnsAgent(AgentHandleId.value handleId)

    let private tryResultsAvailable (items: JoinItem list) =
        NonEmptyBatch.tryOfList items
        |> Option.map (fun batch -> Ok(ResultsAvailable batch))

    let private drainPtyItems (runtime: HostForkRuntime) (count: int) =
        if count <= 0 then
            []
        else
            runtime.Runtime.DrainPtyCompletions count |> List.map JoinItem.ofPtyJoinItem

    let private drainWithoutJournal (runtime: HostForkRuntime) (cap: int) =
        let hasActiveAgents =
            runtime.Runtime.ActiveRunCount > 0
            || lock runtime.Gate (fun () -> runtime.PendingRuns.Count > 0)
            || runtime.Runtime.PendingCompletionCount > runtime.Runtime.PendingPtyCount

        if hasActiveAgents then
            Some(Error(ForkError.NotFound "agent join requires journal; journal=None is fail-closed for agent handles"))
        else
            runtime.Runtime.DrainPtyCompletions cap
            |> List.map JoinItem.ofPtyJoinItem
            |> tryResultsAvailable

    let private drainWithJournal (runtime: HostForkRuntime) (durable: AgentJournal) (cap: int) =
        task {
            let! drained =
                JoinDrain.drainFromJournalWhere
                    durable
                    runtime.ParentId
                    cap
                    (runtime.Clock.UtcNow())
                    (currentProcessHandle runtime)

            match drained with
            | Error e -> return Some(Error e)
            | Ok durableBatch ->
                let remaining = cap - List.length durableBatch
                let agentItems = durableBatch |> List.map JoinItem.ofAgentRunCompletion
                let ptyItems = drainPtyItems runtime remaining
                return tryResultsAvailable (agentItems @ ptyItems)
        }

    let private tryDrainAvailableBody (runtime: HostForkRuntime) (cap: int) =
        ignore (runtime.Runtime.DrainAgentWakes JoinBatch.Max)

        match runtime.Journal with
        | None -> Task.FromResult(drainWithoutJournal runtime cap)
        | Some durable -> drainWithJournal runtime durable cap

    /// EXEC-009 + EXEC-018 + GREEN-5: agent facts from Journal; PTY from mailbox as PtyJoinItem.
    /// journal=None: agent join fail-closed; pure PTY drain still allowed.
    /// Abandoned items join the same ResultsAvailable batch — never withhold
    /// completed results behind a top-level ForkError.Abandoned.
    /// EXEC-020: PTY stays JoinItem/PtyJoinItem until renderer, preserving PtyAborted.
    let private tryDrainAvailable
        (runtime: HostForkRuntime)
        (maxCount: int)
        : Task<Result<JoinWaitOutcome<JoinItem>, ForkError> option> =
        let cap = min maxCount JoinBatch.Max

        if cap <= 0 then
            Task.FromResult None
        else
            tryDrainAvailableBody runtime cap

    let private refuseIfPermitRootMismatch (runtime: HostForkRuntime) (root: SessionId) =
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
            Ok()

    let private refuseIfPermitJournalStale currentSeq permitSeq =
        if currentSeq < permitSeq then
            Error(
                ForkError.NotFound(
                    sprintf "family recovery permit journalSequence stale: permit=%d current=%d" permitSeq currentSeq
                )
            )
        else
            Ok()

    let private refuseIfPermitMembersLost current (permit: FamilyRecoveryPermit) =
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

    let private validatePermitAgainstJournal
        (durable: AgentJournal)
        (root: SessionId)
        permitSeq
        (permit: FamilyRecoveryPermit)
        =
        result {
            let currentSeq = JournalRevision.value (AgentJournal.revision durable)
            do! refuseIfPermitJournalStale currentSeq permitSeq

            let current =
                RecoveryClosureProjection.discover root (AgentJournal.snapshot durable).AgentProjections currentSeq

            // EXEC-023 monotone admission: the permit proves the family it recovered is
            // closed, so what invalidates it is a member GOING MISSING — not the family
            // growing. A child forking a grandchild mid-join changes the closure digest
            // while invalidating no recovery, and comparing digests refused exactly those
            // legitimate joins (`temporal-ownership-unhappy-path`, deterministically).
            do! refuseIfPermitMembersLost current permit
        }

    /// Permit gate shared by JoinAvailableWithPermit / AwaitAgentWithPermit.
    let private validatePermit (runtime: HostForkRuntime) (permit: FamilyRecoveryPermit) : Result<unit, ForkError> =
        let root = FamilyRecoveryPermit.root permit
        let permitSeq = FamilyRecoveryPermit.journalSequence permit

        match refuseIfPermitRootMismatch runtime root, runtime.Journal with
        | Error e, _ -> Error e
        | Ok(), None ->
            Error(
                ForkError.NotFound
                    "family recovery permit requires journal; pure PTY join must not use permit-gated join"
            )
        | Ok(), Some durable -> validatePermitAgainstJournal durable root permitSeq permit

    let private decideWakeReason (reason: MailboxWakeReason) =
        match reason with
        | MailboxCancelled -> Done(Error ForkError.Cancelled)
        | CompletionMayBeAvailable
        | LocalInterrupt _ -> Retry

    let private decideMailboxSignal (signal: MailboxWakeReason) =
        match signal with
        | MailboxCancelled -> Done(Error ForkError.Cancelled)
        | LocalInterrupt reason -> Done(Ok(Interrupted reason))
        | CompletionMayBeAvailable -> Retry

    let private decideJournalRaceWinner (kind: int) (winner: obj) =
        if kind = 0 then
            let reason: MailboxWakeReason = emitJsExpr winner "$0.reason"
            decideWakeReason reason
        elif kind = 2 then
            let interruptReason: JoinInterruptReason = emitJsExpr winner "$0.reason"
            Done(Ok(Interrupted interruptReason))
        else
            Retry

    let private decideFissionRaceWinner (kind: int) (winner: obj) =
        if kind = 1 then
            let reason: JoinInterruptReason = emitJsExpr winner "$0.reason"
            Done(Ok(Interrupted reason))
        else
            Retry

    let private applyJoinPoll (poll: JoinPoll) (retry: unit -> Task<Result<JoinWaitOutcome<JoinItem>, ForkError>>) =
        match poll with
        | Done result -> Task.FromResult result
        | Retry -> retry ()

    let private handleNotRetired (record: HandleRecord) =
        match record.Lifecycle with
        | HandleLifecycle.Retired -> false
        | _ -> true

    let private handleIsActiveJoinTarget (runtime: HostForkRuntime) (record: HandleRecord) =
        currentProcessHandle runtime record && handleNotRetired record

    let private journalHasActiveJoinHandles (runtime: HostForkRuntime) (durable: AgentJournal) =
        AgentJournal.handleProjection durable runtime.ParentId
        |> fun projection ->
            projection.Handles
            |> Map.exists (fun _ record -> handleIsActiveJoinTarget runtime record)

    let private parentHasJoinWork (runtime: HostForkRuntime) =
        runtime.Runtime.ActiveRunCount > 0
        || runtime.Runtime.PendingCompletionCount > 0
        || lock runtime.Gate (fun () -> runtime.PendingRuns.Count > 0 || runtime.PtyRuns.Count > 0)
        || match runtime.Journal with
           | None -> false
           | Some durable -> journalHasActiveJoinHandles runtime durable

    let private raceJournalArms
        (durable: AgentJournal)
        (interrupt: Task<JoinInterruptReason>)
        (runtime: HostForkRuntime)
        =
        task {
            let _, fromRev = durable.SnapshotWithRevision

            // Tagged race arms without nested task{} (dsl-ownership).
            // kind: 0=wake(+reason), 1=journal change, 2=user interrupt.
            let wakeTask: Task<obj> =
                emitJsExpr (runtime.Runtime.WaitForWake()) "$0.then(function (r) { return { kind: 0, reason: r }; })"

            let changeTask: Task<obj> =
                emitJsExpr (durable.AwaitChangeFrom fromRev) "$0.then(function () { return { kind: 1 }; })"

            let interruptTask: Task<obj> =
                emitJsExpr interrupt "$0.then(function (r) { return { kind: 2, reason: r }; })"

            let! winner = emitJsExpr (wakeTask, changeTask, interruptTask) "Promise.race([$0, $1, $2])": Task<obj>

            runtime.Runtime.PulseWake()
            return winner
        }

    let private afterOptionalDrain
        (drained: Result<JoinWaitOutcome<JoinItem>, ForkError> option)
        (onNone: unit -> Task<Result<JoinWaitOutcome<JoinItem>, ForkError>>)
        =
        match drained with
        | Some result -> Task.FromResult result
        | None -> onNone ()

    let private joinAvailableLoop
        (runtime: HostForkRuntime)
        (cap: int)
        (interrupt: Task<JoinInterruptReason>)
        : Task<Result<JoinWaitOutcome<JoinItem>, ForkError>> =
        let rec loop () : Task<Result<JoinWaitOutcome<JoinItem>, ForkError>> =
            task {
                let! drained = tryDrainAvailable runtime cap
                return! afterOptionalDrain drained waitOrFail
            }

        and waitOrFail () =
            if runtime.Runtime.IsCancelled then
                Task.FromResult(Error ForkError.Cancelled)
            elif not (parentHasJoinWork runtime) then
                Task.FromResult(Error ForkError.NothingToJoin)
            else
                waitForSignalThenRetry ()

        and waitForSignalThenRetry () =
            match runtime.Journal with
            | Some durable -> waitWithJournal durable
            | None -> waitWithoutJournal ()

        and waitWithJournal durable =
            task {
                let! drained = tryDrainAvailable runtime cap
                return! afterOptionalDrain drained (fun () -> raceThenDrain durable)
            }

        and raceThenDrain durable =
            task {
                let! winner = raceJournalArms durable interrupt runtime
                let! drained = tryDrainAvailable runtime cap
                return! afterOptionalDrain drained (fun () -> continueFromJournalRace winner)
            }

        and continueFromJournalRace winner =
            let kind: int = emitJsExpr winner "$0.kind"
            applyJoinPoll (decideJournalRaceWinner kind winner) loop

        and waitWithoutJournal () =
            task {
                let! signal = runtime.Runtime.WaitForSignal interrupt
                let! drained = tryDrainAvailable runtime cap
                return! afterOptionalDrain drained (fun () -> applyJoinPoll (decideMailboxSignal signal) loop)
            }

        loop ()

    let private runJoinLoopWithRelease
        (runtime: HostForkRuntime)
        (body: unit -> Task<Result<JoinWaitOutcome<JoinItem>, ForkError>>)
        =
        task {
            try
                return! body ()
            finally
                runtime.ReleaseJoin()
        }

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
        let acquired = runtime.TryAcquireJoin()

        // A tool turn may contain duplicate join calls. Only one waiter may own the
        // runtime wake channels; a second waiter would otherwise consume no fact and
        // remain parked after the first waiter consumes the sole wake.
        if not acquired then
            Task.FromResult(Error ForkError.JoinInProgress)
        elif cap <= 0 then
            runtime.ReleaseJoin()
            Task.FromResult(Error ForkError.Empty)
        else
            // GREEN-4: JoinAvailable does not start recovery; permit path already recovered.
            runJoinLoopWithRelease runtime (fun () -> joinAvailableLoop runtime cap interrupt)

    let private agentAffinityMatchesLane (durable: AgentJournal) (groupId: string) (laneIndex: int) handleId =
        let externalId = FissionExternalId.agent (AgentHandleId.value handleId)

        match FissionProjection.tryGroup groupId (AgentJournal.snapshot durable).AgentProjections.Fission with
        | Some group -> Map.tryFind externalId group.ExternalAffinities = Some laneIndex
        | None -> false

    let private agentHandleAllowedOnLane
        (durable: AgentJournal)
        (groupId: string)
        (laneIndex: int)
        (record: HandleRecord)
        =
        match HandleId.tryAgent record.Handle with
        | None -> false
        | Some handleId -> agentAffinityMatchesLane durable groupId laneIndex handleId

    let private fissionLaneAllowed
        (runtime: HostForkRuntime)
        (durable: AgentJournal)
        (groupId: string)
        (laneIndex: int)
        (record: HandleRecord)
        =
        currentProcessHandle runtime record
        && agentHandleAllowedOnLane durable groupId laneIndex record

    let private fissionLaneHasWork
        (runtime: HostForkRuntime)
        (durable: AgentJournal)
        (groupId: string)
        (laneIndex: int)
        =
        let allowed = fissionLaneAllowed runtime durable groupId laneIndex

        AgentJournal.handleProjection durable runtime.ParentId
        |> fun projection ->
            projection.Handles
            |> Map.exists (fun _ record -> allowed record && handleNotRetired record)

    let private tryDrainFissionLane
        (runtime: HostForkRuntime)
        (durable: AgentJournal)
        (groupId: string)
        (laneIndex: int)
        (cap: int)
        =
        let allowed = fissionLaneAllowed runtime durable groupId laneIndex

        task {
            let! drained = JoinDrain.drainFromJournalWhere durable runtime.ParentId cap (runtime.Clock.UtcNow()) allowed

            match drained with
            | Error error -> return Some(Error error)
            | Ok items ->
                let joined = items |> List.map JoinItem.ofAgentRunCompletion
                return tryResultsAvailable joined
        }

    let private raceFissionArms (durable: AgentJournal) (interrupt: Task<JoinInterruptReason>) =
        task {
            let _, fromRevision = durable.SnapshotWithRevision

            let changeTask: Task<obj> =
                emitJsExpr (durable.AwaitChangeFrom fromRevision) "$0.then(function () { return { kind: 0 }; })"

            let interruptTask: Task<obj> =
                emitJsExpr interrupt "$0.then(function (reason) { return { kind: 1, reason: reason }; })"

            return! emitJsExpr (changeTask, interruptTask) "Promise.race([$0, $1])": Task<obj>
        }

    let private joinFissionLaneLoop
        (runtime: HostForkRuntime)
        (durable: AgentJournal)
        (groupId: string)
        (laneIndex: int)
        (cap: int)
        (interrupt: Task<JoinInterruptReason>)
        =
        let tryDrain () =
            tryDrainFissionLane runtime durable groupId laneIndex cap

        let rec loop () =
            task {
                let! drained = tryDrain ()
                return! afterOptionalDrain drained waitOrFail
            }

        and waitOrFail () =
            if not (fissionLaneHasWork runtime durable groupId laneIndex) then
                Task.FromResult(Error ForkError.NothingToJoin)
            else
                waitThenRetry ()

        and waitThenRetry () =
            task {
                let! drained = tryDrain ()
                return! afterOptionalDrain drained raceThenDrain
            }

        and raceThenDrain () =
            task {
                let! winner = raceFissionArms durable interrupt
                let! drained = tryDrain ()
                return! afterOptionalDrain drained (fun () -> continueFromFissionRace winner)
            }

        and continueFromFissionRace winner =
            let kind: int = emitJsExpr winner "$0.kind"
            applyJoinPoll (decideFissionRaceWinner kind winner) loop

        loop ()

    /// Fission lane join. The lane shares the logical owner's HostForkRuntime,
    /// but its completion drain is affinity-filtered and waits on durable journal
    /// change rather than the owner's shared wake token. That permits sibling
    /// lanes to join distinct handles concurrently without stealing each other's
    /// mailbox wake or completion cell.
    let joinAvailableForFissionLane
        (runtime: HostForkRuntime)
        (groupId: string)
        (laneIndex: int)
        (maxCount: int)
        (interrupt: Task<JoinInterruptReason>)
        : Task<Result<JoinWaitOutcome<JoinItem>, ForkError>> =
        let cap = min (max 0 maxCount) JoinBatch.Max

        match runtime.Journal with
        | None -> Task.FromResult(Error(ForkError.NotFound "Fission lane join requires durable journal"))
        | Some durable when cap <= 0 -> Task.FromResult(Error ForkError.Empty)
        | Some durable -> joinFissionLaneLoop runtime durable groupId laneIndex cap interrupt

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
                let! outcome = awaitAgent runtime agentId timeoutMs
                return Result.mapError ForkError.NotFound outcome
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
