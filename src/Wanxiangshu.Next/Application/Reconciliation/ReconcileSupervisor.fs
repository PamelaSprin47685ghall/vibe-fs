namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Journal

/// Per-session single-flight reconcile supervisor.
/// Idle/retry signals only set a dirty latch; truth comes from the full-message
/// snapshot API. At most three causal yields per kick.
module ReconcileSupervisor =

    type private ReconcileState =
        { mutable Dirty: bool
          mutable Running: bool }

    [<Emit("Promise.resolve()")>]
    let private causalYield: Task<unit> = jsNative

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
            /// Invoked at most once per pass, with the last snapshot actually read. The
            /// attempt loop may read up to three times; observing each read would ask
            /// the same question of three nearly identical snapshots.
            ?onSnapshot: SessionId -> SessionMessage list -> Task
        ) as this =

        let gate = obj ()
        let states = Dictionary<string, ReconcileState>()
        let consumed = Dictionary<string, string>()
        let resolveProjection = defaultArg projection (fun _ -> None)
        let onDeleted = defaultArg onDeleted ignore

        let observeSnapshot =
            defaultArg onSnapshot (fun _ _ -> AsyncSupport.completedTask ())

        let stateOf key =
            match states.TryGetValue(key) with
            | true, state -> state
            | false, _ ->
                let created = { Dirty = false; Running = false }
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

        let alreadyConsumed (turn: ReconciledTurn) =
            let key = SessionId.value turn.SessionId
            let token = consumeKey turn

            match consumed.TryGetValue(key) with
            | true, previous when previous = token -> true
            | _ -> false

        let markConsumed (turn: ReconciledTurn) =
            consumed.[SessionId.value turn.SessionId] <- consumeKey turn

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
                            // I/O. The attempt loop may read up to three times; the last
                            // one is the freshest view and the only one worth observing.
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
                                | Error _ -> ()
                                | Ok messages -> lastSnapshot <- Some messages
                            | Some activeRun ->
                                let mutable continuationCandidate: ReconciledTurn option = None
                                let mutable attempt = 0

                                while attempt < 3 && turnFound.IsNone do
                                    if isCleared sessionId then
                                        attempt <- 3
                                    else
                                        attempt <- attempt + 1

                                        let! snapshotResult = snapshot.GetMessages sessionId

                                        match snapshotResult with
                                        | Error _ -> ()
                                        | Ok messages ->
                                            lastSnapshot <- Some messages

                                            match TurnReconcile.reconcile messages activeRun with
                                            | None -> ()
                                            | Some turn ->
                                                match turn.Outcome with
                                                | TurnCompleted
                                                | TurnAborted _
                                                | TurnFailed _ ->
                                                    turnFound <- Some turn
                                                    continuationCandidate <- None
                                                | TurnInProgress
                                                | TurnNeedsContinuation _ ->
                                                    // Empty/reasoning/contains-XML/tool-call-only
                                                    // snapshots can briefly precede the final parts
                                                    // becoming visible. Keep the candidate but use all
                                                    // causal rereads before deciding to continue the
                                                    // same Logical Run. Continuation is interaction
                                                    // repair, never durable fallback.
                                                    continuationCandidate <- Some turn
                                                | TurnUnknown ->
                                                    // A later explicit Unknown invalidates an earlier
                                                    // provisional candidate; fail closed rather than send
                                                    // from a stale snapshot.
                                                    continuationCandidate <- None

                                        if attempt < 3 && turnFound.IsNone then
                                            do! causalYield

                                if turnFound.IsNone then
                                    turnFound <- continuationCandidate

                            match turnFound with
                            | Some turn ->
                                let publish =
                                    lock gate (fun () ->
                                        if alreadyConsumed turn then
                                            false
                                        else
                                            markConsumed turn
                                            true)

                                if publish then
                                    do! onTurn turn
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
                binding.ClearSession(sessionId)
                onDeleted (sessionId))
