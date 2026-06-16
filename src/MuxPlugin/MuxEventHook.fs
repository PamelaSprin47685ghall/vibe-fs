module VibeFs.MuxPlugin.MuxEventHook

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.Prompts
open VibeFs.Mux.StreamEnd
open VibeFs.Shell.NudgeStore
open VibeFs.MuxPlugin.EventDecoder

type private NudgeRequest =
    { workspaceId: string
      context: NudgeContext
      previousAction: NudgeAction option }

type private HookEvent =
    | Ignore
    | StreamEnd of properties: obj
    | StreamAbort of workspaceId: string
    | AbortedError of workspaceId: string

let private parseHookEvent (event: obj) : HookEvent =
    let decoded = decodeHookEvent event
    if decoded.workspaceId = "" then Ignore
    else
        match decoded.eventType with
        | "stream-end" -> StreamEnd decoded.properties
        | "stream-abort" -> StreamAbort decoded.workspaceId
        | "error" when decoded.errorType = "aborted" -> AbortedError decoded.workspaceId
        | _ -> Ignore

let private tryGetTodos (helpers: obj) (workspaceId: string) : Async<string list> =
    async {
        try
            let getTodosFn = Dyn.get helpers "getTodos"
            let! result = (Dyn.call1 getTodosFn workspaceId :?> JS.Promise<obj array>) |> Async.AwaitPromise
            return result |> Array.map string |> List.ofArray
        with _ ->
            return []
    }

let private previousActionFor (state: StreamEndState) (workspaceId: string) =
    match state.lastNudgeActions.TryGetValue(workspaceId) with
    | true, previous -> Some previous
    | _ -> None

let private buildNudgeRequest (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (state: StreamEndState) (workspaceId: string) (todos: string list) (lastMessage: string) =
    { workspaceId = workspaceId
      context =
        { todos = todos
          lastAssistantMessage = lastMessage
          hasActiveRunner = false
          isLoopActive = reviewStore.isReviewActive workspaceId }
      previousAction = previousActionFor state workspaceId }

let private rememberAction (state: StreamEndState) (workspaceId: string) (action: string) =
    match ofString action with
    | Ok parsed -> state.lastNudgeActions.[workspaceId] <- parsed
    | Error _ -> ()

let private clearWorkspaceState (state: StreamEndState) (workspaceId: string) =
    state.stoppedWorkspaces.Add(workspaceId) |> ignore
    state.retryPendingWorkspaces.Remove(workspaceId) |> ignore
    state.lastNudgeActions.Remove(workspaceId) |> ignore

let private suppressWorkspace (state: StreamEndState) (coordinator: NudgeCoordinator) (workspaceId: string) =
    coordinator.suppress workspaceId
    state.stoppedWorkspaces.Add(workspaceId) |> ignore
    state.lastNudgeActions.Remove(workspaceId) |> ignore

let private handleNudgeRequest (state: StreamEndState) (coordinator: NudgeCoordinator) (helpers: obj) (request: NudgeRequest) : Async<unit> =
    async {
        let action = coordinator.shouldNudge(request.workspaceId, request.context)
        if shouldSuppressNudge request.workspaceId request.context request.previousAction then
            ()
        else
            match selectNudgePrompt action todoNudgePrompt loopNudgePrompt with
            | None -> rememberAction state request.workspaceId action
            | Some prompt ->
                try
                    let nudgeFn = Dyn.get helpers "nudge"
                    let! _ = (Dyn.call2 nudgeFn request.workspaceId prompt :?> JS.Promise<bool>) |> Async.AwaitPromise
                    let previousCount = if state.deliveredCounts.ContainsKey(request.workspaceId) then state.deliveredCounts.[request.workspaceId] else 0
                    state.deliveredCounts.[request.workspaceId] <- previousCount + 1
                    rememberAction state request.workspaceId action
                with _ ->
                    coordinator.clearSession(request.workspaceId)
    }

/// The inner async logic for the event hook.
let private handleEvent (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
                         (state: StreamEndState) (coordinator: NudgeCoordinator)
                         (event: obj) (helpers: obj) : JS.Promise<unit> =
    async {
        match parseHookEvent event with
        | Ignore -> ()
        | StreamEnd properties ->
            let decoded = decodeHookEvent event
            if Dyn.isNullish helpers || state.stoppedWorkspaces.Contains(decoded.workspaceId) then ()
            elif decoded.stopReason = "queued-message" then ()
            else
                let lastMessage = getLastAssistantText properties
                let! todos = tryGetTodos helpers decoded.workspaceId
                let request = buildNudgeRequest reviewStore state decoded.workspaceId todos lastMessage
                do! handleNudgeRequest state coordinator helpers request
        | StreamAbort workspaceId ->
            reviewStore.deactivateReview workspaceId
            coordinator.clearSession(workspaceId)
            clearWorkspaceState state workspaceId
        | AbortedError workspaceId ->
            suppressWorkspace state coordinator workspaceId
    } |> Async.StartAsPromise

/// Create the event hook as a proper two-argument JS function.
/// Uses Func<_,_,_> delegate so Fable emits `function(event, helpers) { ... }`
/// instead of a curried `(event) => (helpers) => ...`.
let createEventHook (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) : obj =
    let state = createStreamEndState ()
    let coordinator = NudgeCoordinator()
    let fn = System.Func<obj, obj, JS.Promise<unit>>(fun event helpers ->
        handleEvent reviewStore state coordinator event helpers)
    box fn
