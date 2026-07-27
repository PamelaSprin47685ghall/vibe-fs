namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

type private SessionReconcileState =
    { mutable Running: bool
      mutable Dirty: bool }

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
            |> List.tryFind (fun message -> message.Role = "assistant")

    let findLatestUser (messages: SessionMessage list) =
        messages
        |> List.rev
        |> List.tryFind (fun message -> message.Role = "user")

    let findLatestBoundTurn (messages: SessionMessage list) (binding: ActiveRunBinding) =
        let userMessageId =
            match binding.UserMessageId with
            | Some id -> Some id
            | None -> findLatestUser messages |> Option.map (fun message -> message.Id)

        match userMessageId with
        | None -> None
        | Some userMessageId ->
            match findAssistantAfter messages userMessageId with
            | None -> None
            | Some assistant ->
                Some(
                    CompletedTurnClassifier.buildTurn
                        binding.SessionId
                        userMessageId
                        assistant
                        binding.AgentRole
                        binding.Directory
                )

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
                        UserMessageId = Some userMessageId }
            | false, _ ->
                bindings.[key] <-
                    { SessionId = sessionId
                      UserMessageId = Some userMessageId
                      AgentRole = None
                      Directory = "" })

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
                lock gate (fun () ->
                    let state = stateOf key
                    state.Dirty <- false)

                let binding = lock gate (fun () -> bindingOf sessionId)

                let active =
                    match binding with
                    | Some value -> Some value
                    | None ->
                        Some
                            { SessionId = sessionId
                              UserMessageId = None
                              AgentRole = None
                              Directory = "" }

                match active with
                | None -> ()
                | Some active ->
                    let! snapshotResult = snapshot.GetMessages sessionId

                    match snapshotResult with
                    | Error _ -> ()
                    | Ok messages ->
                        match findLatestBoundTurn messages active with
                        | None -> ()
                        | Some turn when turn.Outcome = TurnUnknown -> ()
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

                cont <-
                    lock gate (fun () ->
                        let state = stateOf key

                        if state.Dirty then
                            true
                        else
                            state.Running <- false
                            false)
        }

    member this.HandleSignal(signal: HostSignal) =
        match signal with
        | SessionIdle sessionId -> this.MarkDirty sessionId
        | SessionDeleted sessionId -> this.ClearSession sessionId
        | SessionAbort sessionId -> this.MarkDirty sessionId
        | ProviderRetry _ -> ()
