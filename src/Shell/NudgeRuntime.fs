module VibeFs.Shell.NudgeRuntime

open Fable.Core
open VibeFs.Kernel
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.Prompts

/// Decoded event ready for stateful processing. Host adapters (e.g.
/// Mux/EventHook) produce this from their native event shapes; NudgeRuntime
/// owns the state transitions and the nudge coordinator.
type NudgeRuntimeEvent =
    | Ignore
    | StreamEnd of workspaceId: string * stopReason: string * lastAssistantMessage: string
    | StreamAbort of workspaceId: string
    | AbortedError of workspaceId: string

type private StreamEndState =
    { stoppedWorkspaces: Set<string>
      lastNudgeActions: Map<string, NudgeAction> }

let private freshStreamEndState : StreamEndState =
    { stoppedWorkspaces = Set.empty
      lastNudgeActions = Map.empty }

let private selectNudgePrompt (action: string) : string option =
    match action with
    | "nudge-todo" -> Some todoNudgePrompt
    | "nudge-loop" -> Some loopNudgePrompt
    | _ -> None

let private tryGetTodos (helpers: obj) (workspaceId: string) : JS.Promise<string list> =
    promise {
        try
            let getTodosFn = Dyn.get helpers "getTodos"
            let! result = unbox<JS.Promise<obj[]>> (Dyn.call1 getTodosFn workspaceId)
            return result |> Array.map string |> List.ofArray
        with _ ->
            return []
    }

let private rememberAction (state: StreamEndState) (workspaceId: string) (action: string) : StreamEndState =
    match ofString action with
    | Ok parsed -> { state with lastNudgeActions = Map.add workspaceId parsed state.lastNudgeActions }
    | Error _ -> state

let private clearWorkspaceState (state: StreamEndState) (workspaceId: string) : StreamEndState =
    { stoppedWorkspaces = Set.add workspaceId state.stoppedWorkspaces
      lastNudgeActions = Map.remove workspaceId state.lastNudgeActions }

let private handleNudgeRequest
    (isReviewActive: string -> bool)
    (coordinator: CoordinatorRuntimeState ref)
    (state: StreamEndState)
    (helpers: obj)
    (workspaceId: string)
    (lastMessage: string)
    (todos: string list)
    : JS.Promise<StreamEndState> =
    promise {
        let context : NudgeContext =
            { todos = todos
              lastAssistantMessage = lastMessage
              hasActiveRunner = false
              isLoopActive = isReviewActive workspaceId }
        let previousAction = Map.tryFind workspaceId state.lastNudgeActions
        let nextCoordinator, action = decideRuntimeAction coordinator.Value workspaceId context
        coordinator.Value <- nextCoordinator
        if shouldSuppressNudge workspaceId context previousAction then
            return state
        else
            match selectNudgePrompt action with
            | None -> return rememberAction state workspaceId action
            | Some prompt ->
                try
                    let nudgeFn = Dyn.get helpers "nudge"
                    let! _ = unbox<JS.Promise<bool>> (Dyn.call2 nudgeFn workspaceId prompt)
                    return rememberAction state workspaceId action
                with _ ->
                    coordinator.Value <- clearRuntimeSession coordinator.Value workspaceId
                    return state
    }

/// Holds the per-workspace stream-end state and the nudge coordinator. All
/// mutations happen inside HandleEvent; the host adapter only decodes events
/// into NudgeRuntimeEvent and delegates.
type NudgeRuntime(reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) =
    let mutable state = freshStreamEndState
    let coordinator = ref freshCoordinatorRuntime

    member _.HandleEvent(parsed: NudgeRuntimeEvent, helpers: obj) : JS.Promise<unit> =
        promise {
            match parsed with
            | Ignore -> return ()
            | StreamEnd(workspaceId, stopReason, lastMessage) ->
                if Dyn.isNullish helpers || Set.contains workspaceId state.stoppedWorkspaces then
                    return ()
                elif stopReason = "queued-message" then
                    return ()
                else
                    let! todos = tryGetTodos helpers workspaceId
                    let! nextState =
                        handleNudgeRequest reviewStore.isReviewActive coordinator state helpers workspaceId lastMessage todos
                    state <- nextState
                    return ()
            | StreamAbort workspaceId ->
                reviewStore.deactivateReview workspaceId
                coordinator.Value <- clearRuntimeSession coordinator.Value workspaceId
                state <- clearWorkspaceState state workspaceId
                return ()
            | AbortedError workspaceId ->
                coordinator.Value <- suppressSession coordinator.Value workspaceId
                state <- clearWorkspaceState state workspaceId
                return ()
        }

let createNudgeRuntime (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) : NudgeRuntime =
    NudgeRuntime(reviewStore)
