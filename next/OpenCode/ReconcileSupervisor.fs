namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
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
            onTurn: ReconciledTurn -> unit,
            ?onDeleted: SessionId -> unit,
            ?projection: (SessionId -> AgentProjectionSet option)
        ) as this =

        let gate = obj ()
        let states = Dictionary<string, ReconcileState>()
        let consumed = Dictionary<string, string>()
        let resolveProjection = defaultArg projection (fun _ -> None)
        let onDeleted = defaultArg onDeleted ignore

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
                   MessageId.value turn.UserMessageId
                   "|"
                   MessageId.value turn.AssistantMessageId |]
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

                            match active with
                            | None -> () // Unknown origin: no decision.
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
                                    onTurn turn
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
        member _.Signal(signal: HostSignal) : unit =
            match signal with
            | SessionIdle sessionId -> this.Kick(sessionId)
            | SessionDeleted sessionId -> this.ClearSession(sessionId)
            | ProviderRetry _
            | ProviderError _ -> ()

        /// Bind a new authority root (human or agent owner).
        member _.BindUserMessage(sessionId: SessionId, messageId: MessageId, ?agentRole: AgentRole) =
            binding.BindUserMessage(sessionId, messageId, ?agentRole = agentRole)

        /// Bind a continuation physical message to the active logical run.
        member _.BindContinuationUserMessage(sessionId: SessionId, messageId: MessageId) =
            binding.BindContinuationUserMessage(sessionId, messageId)

        /// Register a host-provided active run.
        member _.BindActiveRun(b: ActiveRunBinding) = binding.BindActiveRun(b)

        /// Latest physical user message for the active logical run.
        member _.TryPhysicalUserMessage(sessionId: SessionId) : MessageId option =
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
