namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open Wanxiangshu.Journal

/// Per-session single-flight reconcile supervisor.
/// Idle/retry signals only set a dirty latch; truth comes from the full-message
/// snapshot API. An active-run pass rereads with real timer backoff until a
/// terminal outcome appears, the wall-clock budget is exhausted, or the session
/// is cleared — so a late transcript does not require a second Host signal.
module ReconcileSupervisor =

    type private ReconcileState =
        {
            mutable Dirty: bool
            mutable Running: bool
        }

    /// Fable-safe timer delay. Not `Task.Delay`: Fable does not export it, and a
    /// module-level Promise would fire once at load if written as a value.
    let private delayMs (ms: int) : Task<unit> =
        if ms <= 0 then
            Task.FromResult(())
        else
            emitJsExpr ms "new Promise(res => setTimeout(res, $0))"

    /// Production backoff between snapshot rereads while terminal material is
    /// still invisible (ms). After the last entry the delay stays at 5s until
    /// the wall-clock budget is exhausted.
    let private productionBackoffDelaysMs =
        [| 50; 100; 250; 500; 1000; 2000; 3000; 5000 |]

    /// Wall-clock upper bound for one active-run materialization pass (ms).
    let private productionMaxBudgetMs = 30_000

    [<Emit("console.error($0, $1)")>]
    let private logError (prefix: string) (message: string) : unit = jsNative

    let private isTerminalOutcome (outcome: TurnOutcome) =
        match outcome with
        | TurnCompleted
        | TurnAborted _
        | TurnFailed _ -> true
        | TurnInProgress
        | TurnNeedsContinuation _
        | TurnUnknown -> false

    let private pickDelay (sequence: int array) (index: int) (budgetRemaining: int) =
        if sequence.Length = 0 || budgetRemaining <= 0 then
            0
        else
            let raw = sequence.[min index (sequence.Length - 1)]
            min raw budgetRemaining

    type Supervisor
        (
            snapshot: ISessionSnapshotPort,
            binding: TurnBinding.Store,
            /// Returns a Task so the pass awaits the turn's effects.
            ///
            /// `ReconciledTurn -> unit` forced the consumer to discard its own
            /// task, and FALLBACK-003 put the durable cursor advance inside it —
            /// so a failed turn's advance raced the next reconcile pass instead of
            /// preceding it.
            onTurn: ReconciledTurn -> Task,
            ?onDeleted: SessionId -> unit,
            ?projection: (SessionId -> AgentProjectionSet option),
            /// HOST-006 containment: the full message list this pass read.
            ///
            /// A separate observer from `onTurn` because it answers a different
            /// question. `onTurn` is about the active Logical Run's outcome; this is
            /// about what appeared in the transcript regardless of any run — and a Host
            /// compaction pseudo-run belongs to no Logical Run of ours, so it would
            /// never reach a turn-shaped callback.
            ///
            /// Invoked at most once per pass, with the last snapshot actually read.
            ?onSnapshot: SessionId -> SessionMessage list -> Task,
            /// Optional backoff delays (ms) for active-run rereads. Defaults to
            /// productionBackoffDelaysMs. Tests inject short arrays so the full
            /// budget stays under PER_TEST_TIMEOUT_MS.
            ?backoffDelaysMs: int array,
            /// Optional wall-clock budget (ms) for one active-run pass. Defaults
            /// to productionMaxBudgetMs. Tests inject a small value so always-
            /// incomplete scripts end without waiting the production 30s.
            ?maxBudgetMs: int
        ) as this =

        let gate = obj ()
        let states = Dictionary<string, ReconcileState>()
        /// Terminal outcomes only. Non-terminal publishes use `provisional` so a
        /// later terminal with the same run identity is not sealed out.
        let consumed = Dictionary<string, string>()
        /// Non-terminal publish tokens (session → consume key). Once per incomplete
        /// episode so trailing passes do not spam interaction repair.
        let provisional = Dictionary<string, string>()
        let resolveProjection = defaultArg projection (fun _ -> None)
        let onDeleted = defaultArg onDeleted ignore

        let backoffDelaysMs =
            defaultArg backoffDelaysMs productionBackoffDelaysMs

        let maxBudgetMs =
            defaultArg maxBudgetMs productionMaxBudgetMs

        let observeSnapshot =
            defaultArg onSnapshot (fun _ _ -> AsyncSupport.completedTask ())

        let stateOf key =
            match states.TryGetValue(key) with
            | true, state -> state
            | false, _ ->
                let created =
                    { Dirty = false
                      Running = false }

                states.[key] <- created
                created

        let consumeKey (turn: ReconciledTurn) =
            String.Concat(
                [| SessionId.value turn.SessionId
                   "|"
                   PhysicalUserMessageId.value turn.PhysicalUserMessageId
                   "|"
                   ProviderRunIdentity.value turn.ProviderRun |]
            )

        let clearProvisional (sessionKey: string) =
            provisional.Remove(sessionKey) |> ignore

        let alreadyPublished (turn: ReconciledTurn) =
            let key = SessionId.value turn.SessionId
            let token = consumeKey turn

            if isTerminalOutcome turn.Outcome then
                match consumed.TryGetValue(key) with
                | true, previous when previous = token -> true
                | _ -> false
            else
                match provisional.TryGetValue(key) with
                | true, previous when previous = token -> true
                | _ -> false

        let markPublished (turn: ReconciledTurn) =
            let key = SessionId.value turn.SessionId
            let token = consumeKey turn

            if isTerminalOutcome turn.Outcome then
                consumed.[key] <- token
                provisional.Remove(key) |> ignore
            else
                provisional.[key] <- token

        let beginPass (sessionId: SessionId) =
            lock gate (fun () ->
                match states.TryGetValue(SessionId.value sessionId) with
                | false, _ -> false
                | true, state ->
                    // Consume exactly the wake that started this pass. A later
                    // idle arriving during snapshot I/O sets Dirty again and is
                    // therefore observed by the trailing-pass check.
                    state.Dirty <- false
                    true)

        let isCleared (sessionId: SessionId) =
            lock gate (fun () -> not (states.ContainsKey(SessionId.value sessionId)))

        let rec runLoop (sessionId: SessionId) : Task =
            task {
                let key = SessionId.value sessionId
                let mutable releaseOnExit = true

                try
                    let mutable cont = true

                    while cont do
                        if not (beginPass sessionId) then
                            releaseOnExit <- false
                            cont <- false
                        else
                            let active =
                                binding.ActiveRunBinding(sessionId, ?projection = resolveProjection sessionId)

                            let mutable turnFound: ReconciledTurn option = None

                            // HOST-006: the last snapshot this pass actually read.
                            //
                            // Captured rather than re-fetched, so observing costs no extra
                            // I/O. The backoff loop may read many times; the last one is
                            // the freshest view and the only one worth observing.
                            let mutable lastSnapshot: SessionMessage list option = None

                            match active with
                            | None ->
                                // Unknown origin: no turn decision (PROMPT-004 fails
                                // closed). But a Host compaction pseudo-run belongs to no
                                // Logical Run of ours, so it can appear precisely here —
                                // a manual `/compact` on a session the plugin has not
                                // bound a root for. Skipping the read would make that
                                // compaction invisible until some unrelated turn happened
                                // to wake this loop.
                                let! snapshotResult = snapshot.GetMessages sessionId

                                match snapshotResult with
                                | Error err ->
                                    logError "RECONCILE-SNAPSHOT" (sprintf "pass snapshot failed: %s" (string err))
                                | Ok messages -> lastSnapshot <- Some messages
                            | Some activeRun ->
                                let mutable continuationCandidate: ReconciledTurn option = None
                                let mutable budgetRemaining = maxBudgetMs
                                let mutable backoffIndex = 0
                                let mutable terminalFound = false

                                while not terminalFound && budgetRemaining > 0 && not (isCleared sessionId) do
                                    let! snapshotResult = snapshot.GetMessages sessionId

                                    match snapshotResult with
                                    | Error err ->
                                        logError
                                            "RECONCILE-SNAPSHOT"
                                            (sprintf "attempt snapshot failed: %s" (string err))

                                        // Snapshot errors do not end the pass: retry with
                                        // backoff until budget or clear. Do not reset the
                                        // index — consecutive errors escalate the delay.
                                        let delay = pickDelay backoffDelaysMs backoffIndex budgetRemaining

                                        if delay > 0 && not (isCleared sessionId) then
                                            do! delayMs delay
                                            budgetRemaining <- budgetRemaining - delay

                                        backoffIndex <- backoffIndex + 1
                                    | Ok messages ->
                                        // Successful I/O resets escalation so a late
                                        // transcript is probed at the short end of the
                                        // sequence after a prior error streak.
                                        backoffIndex <- 0
                                        lastSnapshot <- Some messages

                                        match TurnReconcile.reconcile messages activeRun with
                                        | None ->
                                            let delay =
                                                pickDelay backoffDelaysMs backoffIndex budgetRemaining

                                            if delay > 0 && not (isCleared sessionId) then
                                                do! delayMs delay
                                                budgetRemaining <- budgetRemaining - delay

                                            backoffIndex <- backoffIndex + 1
                                        | Some turn ->
                                            match turn.Outcome with
                                            | TurnCompleted
                                            | TurnAborted _
                                            | TurnFailed _ ->
                                                turnFound <- Some turn
                                                continuationCandidate <- None
                                                terminalFound <- true
                                            | TurnInProgress
                                            | TurnNeedsContinuation _ ->
                                                // Empty/reasoning/contains-XML/tool-call-only
                                                // snapshots can briefly precede the final parts
                                                // becoming visible. Keep the candidate and keep
                                                // rereading until terminal, budget, or clear.
                                                // Continuation is interaction repair, never
                                                // durable fallback.
                                                continuationCandidate <- Some turn

                                                let delay =
                                                    pickDelay backoffDelaysMs backoffIndex budgetRemaining

                                                if delay > 0 && not (isCleared sessionId) then
                                                    do! delayMs delay
                                                    budgetRemaining <- budgetRemaining - delay

                                                backoffIndex <- backoffIndex + 1
                                            | TurnUnknown ->
                                                // A later explicit Unknown invalidates an earlier
                                                // provisional candidate; fail closed rather than send
                                                // from a stale snapshot.
                                                continuationCandidate <- None

                                                let delay =
                                                    pickDelay backoffDelaysMs backoffIndex budgetRemaining

                                                if delay > 0 && not (isCleared sessionId) then
                                                    do! delayMs delay
                                                    budgetRemaining <- budgetRemaining - delay

                                                backoffIndex <- backoffIndex + 1

                                if turnFound.IsNone then
                                    turnFound <- continuationCandidate

                            match turnFound with
                            | Some turn ->
                                let publish =
                                    lock gate (fun () ->
                                        if alreadyPublished turn then
                                            false
                                        else
                                            markPublished turn
                                            true)

                                if publish then
                                    do! onTurn turn

                                if isTerminalOutcome turn.Outcome then
                                    lock gate (fun () -> clearProvisional key)
                            | None -> ()

                            // HOST-006 containment, after the turn.
                            //
                            // Order matters. The turn's own effects — FALLBACK-003's
                            // cursor advance, a Companion commit — belong to the epoch
                            // that was in force when the request was made. A reanchor
                            // retires that epoch, so running it first would make those
                            // effects land against a prefix generation the request never
                            // used.
                            match lastSnapshot with
                            | Some messages -> do! observeSnapshot sessionId messages
                            | None -> ()

                            cont <-
                                lock gate (fun () ->
                                    match states.TryGetValue(key) with
                                    | false, _ ->
                                        releaseOnExit <- false
                                        false
                                    | true, state when state.Dirty ->
                                        // A real coarse signal arrived during this pass.
                                        // Keep Running=true and consume it in one trailing pass.
                                        true
                                    | true, state ->
                                        state.Running <- false
                                        releaseOnExit <- false
                                        false)
                finally
                    // Only the exceptional path still owns Running here. A
                    // normal release may already have allowed a new run to
                    // start, so it must never be overwritten in this finally.
                    if releaseOnExit then
                        lock gate (fun () ->
                            match states.TryGetValue(key) with
                            | true, state -> state.Running <- false
                            | false, _ -> ())
            }

        /// Mark dirty and return true if this call should start the run loop.
        member private _.MarkDirty(sessionId: SessionId) : bool =
            let key = SessionId.value sessionId

            lock gate (fun () ->
                let state = stateOf key
                state.Dirty <- true

                if state.Running then
                    false
                else
                    state.Running <- true
                    true)

        /// Kick a reconcile for `sessionId` if not already in flight.
        member _.Kick(sessionId: SessionId) : unit =
            if this.MarkDirty(sessionId) then
                runLoop sessionId |> ignore

        /// Handle a coarse host signal.
        ///
        /// FALLBACK-003: every signal is a wake, nothing more. Retry and failure
        /// carry no decision — the reconciled snapshot is the only source for
        /// whether the attempt actually failed, and FallbackController is the only
        /// writer of the cursor advance that follows.
        member _.Signal(signal: HostSignal) : unit =
            match signal with
            | SessionIdle sessionId
            | ProviderFailure(sessionId, _) -> this.Kick(sessionId)
            | ProviderRetry retry -> this.Kick(retry.SessionId)
            | SessionDeleted sessionId -> this.ClearSession(sessionId)

        /// Bind a new authority root (human or agent owner).
        member _.BindUserMessage(sessionId: SessionId, physical: PhysicalUserMessageId, ?agentRole: AgentRole) =
            binding.BindUserMessage(sessionId, physical, ?agentRole = agentRole)

        /// Bind a continuation physical message to the active logical run.
        member _.BindContinuationUserMessage(sessionId: SessionId, physical: PhysicalUserMessageId) =
            binding.BindContinuationUserMessage(sessionId, physical)

        /// Register a host-provided active run.
        member _.BindActiveRun(b: ActiveRunBinding) = binding.BindActiveRun(b)

        /// Latest physical user message for the active logical run.
        member _.TryPhysicalUserMessage(sessionId: SessionId) : PhysicalUserMessageId option =
            binding.TryPhysicalUserMessage(sessionId)

        /// Root user message bindings used for fallback identity.
        member _.RootBindings = binding.UserMessageBindings

        /// Remove all state for a session and drop any in-flight reconcile.
        member _.ClearSession(sessionId: SessionId) : unit =
            lock gate (fun () ->
                let key = SessionId.value sessionId
                states.Remove(key) |> ignore
                consumed.Remove(key) |> ignore
                provisional.Remove(key) |> ignore
                binding.ClearSession(sessionId)
                onDeleted (sessionId))
