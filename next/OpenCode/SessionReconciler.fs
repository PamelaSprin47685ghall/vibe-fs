namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

type private SessionReconcileState =
    { mutable Running: bool
      mutable Dirty: bool }

module private SessionReconcilerAsync =
    [<Emit("Promise.resolve()")>]
    let causalYield: Task<unit> = jsNative

/// Idle is only a dirty mark. Truth comes from the full-message snapshot API.
/// Single-flight: concurrent idle collapses into at most one in-flight query
/// plus one trailing rerun.
type SessionReconciler
    (
        snapshot: ISessionSnapshotPort,
        onTurn: ReconciledTurn -> unit,
        ?resolveBinding: SessionId -> ActiveRunBinding option,
        ?onDeleted: SessionId -> unit
    ) =

    let gate = obj ()
    let states = Dictionary<string, SessionReconcileState>()
    let consumed = Dictionary<string, string>()
    let bindings = Dictionary<string, ActiveRunBinding>()
    let resolveBinding = defaultArg resolveBinding (fun _ -> None)
    let onDeleted = defaultArg onDeleted ignore

    let stateOf sessionKey =
        match states.TryGetValue sessionKey with
        | true, state -> state
        | false, _ ->
            let created = { Running = false; Dirty = false }
            states.[sessionKey] <- created
            created

    let findAssistantAfter (messages: SessionMessage list) (userMessageId: MessageId) =
        let userId = MessageId.value userMessageId

        let rec skipUntilUser remaining =
            match remaining with
            | [] -> None
            | head :: tail when MessageId.value head.Id = userId -> Some tail
            | _ :: tail -> skipUntilUser tail

        match skipUntilUser messages with
        | None -> None
        | Some afterUser ->
            afterUser
            |> List.filter (fun message -> message.Role = "assistant")
            |> List.tryLast

    let findLatestUser (messages: SessionMessage list) =
        messages |> List.rev |> List.tryFind (fun message -> message.Role = "user")

    let findLatestBoundTurn (messages: SessionMessage list) (binding: ActiveRunBinding) =
        let rootUserMessageId =
            match binding.RootUserMessageId with
            | Some id -> Some id
            | None -> findLatestUser messages |> Option.map (fun message -> message.Id)

        let physicalUserMessageId =
            binding.PhysicalUserMessageId |> Option.orElse rootUserMessageId

        match rootUserMessageId, physicalUserMessageId with
        | Some rootUserMessageId, Some physicalUserMessageId ->
            let assistant =
                findAssistantAfter messages physicalUserMessageId
                // Some Host prompt_async variants acknowledge queue admission
                // with a synthetic ID. Keep that ID as the continuation's
                // causal identity for tools, but reconcile terminal output from
                // the authority-root transcript when the synthetic ID is not a
                // persisted user message.
                |> Option.orElseWith (fun () ->
                    if physicalUserMessageId = rootUserMessageId then
                        None
                    else
                        findAssistantAfter messages rootUserMessageId)

            match assistant with
            | None -> None
            | Some assistant ->
                Some(
                    CompletedTurnClassifier.buildTurn
                        binding.SessionId
                        physicalUserMessageId
                        rootUserMessageId
                        assistant
                        binding.AgentRole
                        binding.Directory
                )
        | _ -> None

    let consumeKey (turn: ReconciledTurn) =
        sprintf
            "%s|%s|%s"
            (SessionId.value turn.SessionId)
            (MessageId.value turn.UserMessageId)
            (MessageId.value turn.AssistantMessageId)

    let alreadyConsumed (turn: ReconciledTurn) =
        let key = SessionId.value turn.SessionId
        let token = consumeKey turn

        match consumed.TryGetValue key with
        | true, previous when previous = token -> true
        | _ -> false

    let markConsumed (turn: ReconciledTurn) =
        consumed.[SessionId.value turn.SessionId] <- consumeKey turn

    let bindingOf (sessionId: SessionId) =
        let key = SessionId.value sessionId

        match bindings.TryGetValue key with
        | true, binding -> Some binding
        | false, _ -> resolveBinding sessionId

    member _.BindActiveRun(binding: ActiveRunBinding) =
        lock gate (fun () -> bindings.[SessionId.value binding.SessionId] <- binding)

    member _.BindUserMessage(sessionId: SessionId, userMessageId: MessageId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            match bindings.TryGetValue key with
            | true, binding ->
                bindings.[key] <-
                    { binding with
                        RootUserMessageId = Some userMessageId
                        PhysicalUserMessageId = Some userMessageId }
            | false, _ ->
                bindings.[key] <-
                    { SessionId = sessionId
                      RunId = None
                      RootUserMessageId = Some userMessageId
                      PhysicalUserMessageId = Some userMessageId
                      ContinuationMessageIds = Set.empty
                      AgentRole = None
                      Directory = "" })

    member _.BindContinuationUserMessage(sessionId: SessionId, userMessageId: MessageId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            match bindings.TryGetValue key with
            | true, binding ->
                bindings.[key] <-
                    { binding with
                        PhysicalUserMessageId = Some userMessageId
                        ContinuationMessageIds = binding.ContinuationMessageIds.Add(MessageId.value userMessageId) }
            | false, _ -> ())

    member _.TryPhysicalUserMessage(sessionId: SessionId) =
        lock gate (fun () ->
            match bindings.TryGetValue(SessionId.value sessionId) with
            | true, binding -> binding.PhysicalUserMessageId
            | false, _ -> None)

    member _.ClearSession(sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            bindings.Remove key |> ignore
            consumed.Remove key |> ignore
            states.Remove key |> ignore)

        onDeleted sessionId

    member this.MarkDirty(sessionId: SessionId) =
        let key = SessionId.value sessionId

        let shouldStart =
            lock gate (fun () ->
                let state = stateOf key
                state.Dirty <- true

                if state.Running then
                    false
                else
                    state.Running <- true
                    true)

        if shouldStart then
            this.RunLoop(sessionId) |> ignore

    member private _.RunLoop(sessionId: SessionId) =
        task {
            let key = SessionId.value sessionId
            let mutable cont = true

            while cont do
                let isCleared = lock gate (fun () -> not (states.ContainsKey key))

                if isCleared then
                    cont <- false
                else
                    lock gate (fun () ->
                        let state = stateOf key
                        state.Dirty <- false)

                    let binding = lock gate (fun () -> bindingOf sessionId)

                    let active =
                        match binding with
                        | Some value -> value
                        | None ->
                            { SessionId = sessionId
                              RunId = None
                              RootUserMessageId = None
                              PhysicalUserMessageId = None
                              ContinuationMessageIds = Set.empty
                              AgentRole = None
                              Directory = "" }

                    let mutable turnFound: ReconciledTurn option = None
                    let mutable attempt = 0

                    // Bounded causal reread (SSOT: "只要 API snapshot version 有因果
                    // 进展即可继续"). Each snapshot.GetMessages call is itself an
                    // async wait (a genuine host/network round trip in production).
                    // Between unsuccessful attempts we yield to the event loop so
                    // the API snapshot has a causal chance to advance; this is not
                    // a wall-clock delay.
                    while attempt < 3 && turnFound.IsNone do
                        let cleared = lock gate (fun () -> not (states.ContainsKey key))

                        if cleared then
                            attempt <- 3
                        else
                            attempt <- attempt + 1
                            let! snapshotResult = snapshot.GetMessages sessionId

                            match snapshotResult with
                            | Error _ -> ()
                            | Ok messages ->
                                match findLatestBoundTurn messages active with
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
                                do! SessionReconcilerAsync.causalYield

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

                        lock gate (fun () ->
                            let state = stateOf key
                            state.Dirty <- false)

                    | None ->
                        lock gate (fun () ->
                            let state = stateOf key
                            state.Dirty <- true)

                    cont <-
                        lock gate (fun () ->
                            match states.TryGetValue key with
                            | false, _ -> false
                            | true, state ->
                                if state.Dirty && turnFound.IsSome then
                                    true
                                else
                                    state.Running <- false
                                    false)
        }

    member this.HandleSignal(signal: HostSignal) =
        match signal with
        | SessionIdle sessionId -> this.MarkDirty sessionId
        | SessionDeleted sessionId -> this.ClearSession sessionId
        | ProviderRetry _ -> ()
