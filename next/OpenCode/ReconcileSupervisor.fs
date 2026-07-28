namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Journal

/// Per-session single-flight reconcile supervisor.
/// `dirty` latch (HashSet) + `inFlight: Map<SessionId, Task>`.
/// At most three causal yields per kick.
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
        let inFlight = Dictionary<string, Task>()
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

        let setDirty (sessionId: SessionId) (value: bool) =
            lock gate (fun () ->
                let key = SessionId.value sessionId

                if states.ContainsKey(key) then
                    (stateOf key).Dirty <- value)

        let isCleared (sessionId: SessionId) =
            lock gate (fun () -> not (states.ContainsKey(SessionId.value sessionId)))

        let rec runLoop (sessionId: SessionId) : Task =
            task {
                let key = SessionId.value sessionId

                try
                    let mutable cont = true

                    while cont do
                        if isCleared sessionId then
                            cont <- false
                        else
                            setDirty sessionId false

                            let binding =
                                binding.ActiveRunBinding(sessionId, ?projection = resolveProjection sessionId)

                            match binding with
                            | None ->
                                // Unknown origin: keep dirty for the next signal.
                                setDirty sessionId true
                                cont <- false
                            | Some active ->
                                let mutable turnFound: ReconciledTurn option = None
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
                                            match TurnReconcile.reconcile messages active with
                                            | Some turn when
                                                (match turn.Outcome with
                                                 | TurnCompleted
                                                 | TurnAborted _
                                                 | TurnFailed _ -> true
                                                 | _ -> false)
                                                ->
                                                turnFound <- Some turn
                                            | _ -> ()

                                        if attempt < 3 && turnFound.IsNone then
                                            do! causalYield

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

                                    setDirty sessionId false

                                | None ->
                                    // Unknown / in-progress: remain dirty for the
                                    // next coarse signal.  Do not spin.
                                    setDirty sessionId true

                                cont <-
                                    lock gate (fun () ->
                                        match states.TryGetValue(key) with
                                        | false, _ -> false
                                        | true, state ->
                                            if state.Dirty && turnFound.IsSome then
                                                true
                                            else
                                                state.Running <- false
                                                false)
                finally
                    lock gate (fun () ->
                        inFlight.Remove(key) |> ignore

                        if states.ContainsKey(key) then
                            (stateOf key).Running <- false)
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
                let task = runLoop sessionId

                lock gate (fun () -> inFlight.[SessionId.value sessionId] <- task)

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
                inFlight.Remove(key) |> ignore
                consumed.Remove(key) |> ignore
                binding.ClearSession(sessionId)
                onDeleted (sessionId))
