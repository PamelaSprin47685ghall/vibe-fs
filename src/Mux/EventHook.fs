module VibeFs.Mux.EventHook

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.Prompts

type private NudgeRequest =
    { workspaceId: string
      context: NudgeContext
      previousAction: NudgeAction option }

type private DecodedHookEvent =
    { eventType: string
      workspaceId: string
      properties: obj
      metadata: obj
      stopReason: string
      errorType: string }

type private HookEvent =
    | Ignore
    | StreamEnd of properties: obj
    | StreamAbort of workspaceId: string
    | AbortedError of workspaceId: string

type private StreamEndState =
    { stoppedWorkspaces: Set<string>
      lastNudgeActions: Map<string, NudgeAction> }

let private selectNudgePrompt (action: string) (todoPrompt: string) (loopPrompt: string) : string option =
    match action with
    | "nudge-todo" -> Some todoPrompt
    | "nudge-loop" -> Some loopPrompt
    | _ -> None

let private createStreamEndState () : StreamEndState =
    { stoppedWorkspaces = Set.empty
      lastNudgeActions = Map.empty }

let private decodeHookEvent (event: obj) : DecodedHookEvent =
    let props = Dyn.get event "properties"
    let meta = if Dyn.isNullish props then null else Dyn.get props "metadata"
    { eventType = if Dyn.isNullish event then "" else Dyn.str event "type"
      workspaceId = Dyn.str event "workspaceId"
      properties = if Dyn.isNullish props then null else props
      metadata = meta
      stopReason = if Dyn.isNullish meta then "" else Dyn.str meta "muxStopReason"
      errorType = if Dyn.isNullish props then "" else Dyn.str props "errorType" }

let private getLastAssistantText (properties: obj) : string =
    if Dyn.isNullish properties then ""
    else
        let parts = Dyn.get properties "parts"
        if Dyn.isNullish parts || not (Dyn.isArray parts) then ""
        else
            (parts :?> obj array)
            |> Array.filter (fun p -> Dyn.str p "type" = "text")
            |> Array.map (fun p -> Dyn.str p "text")
            |> String.concat "\n"

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
    Map.tryFind workspaceId state.lastNudgeActions

let private buildNudgeRequest (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (state: StreamEndState) (workspaceId: string) (todos: string list) (lastMessage: string) =
    { workspaceId = workspaceId
      context =
        { todos = todos
          lastAssistantMessage = lastMessage
          hasActiveRunner = false
          isLoopActive = reviewStore.isReviewActive workspaceId }
      previousAction = previousActionFor state workspaceId }

let private rememberAction (state: StreamEndState) (workspaceId: string) (action: string) : StreamEndState =
    match ofString action with
    | Ok parsed -> { state with lastNudgeActions = Map.add workspaceId parsed state.lastNudgeActions }
    | Error _ -> state

let private clearWorkspaceState (state: StreamEndState) (workspaceId: string) : StreamEndState =
    { state with
        stoppedWorkspaces = Set.add workspaceId state.stoppedWorkspaces
        lastNudgeActions = Map.remove workspaceId state.lastNudgeActions }

let private suppressWorkspace (state: StreamEndState) (coordinator: CoordinatorRuntimeState ref) (workspaceId: string) : StreamEndState =
    coordinator.Value <- suppressSession coordinator.Value workspaceId
    { state with
        stoppedWorkspaces = Set.add workspaceId state.stoppedWorkspaces
        lastNudgeActions = Map.remove workspaceId state.lastNudgeActions }

let private handleNudgeRequest (state: StreamEndState) (coordinator: CoordinatorRuntimeState ref) (helpers: obj) (request: NudgeRequest) : Async<StreamEndState> =
    async {
        let nextCoordinator, action = decideRuntimeAction coordinator.Value request.workspaceId request.context
        coordinator.Value <- nextCoordinator
        if shouldSuppressNudge request.workspaceId request.context request.previousAction then
            return state
        else
            match selectNudgePrompt action todoNudgePrompt loopNudgePrompt with
            | None -> return rememberAction state request.workspaceId action
            | Some prompt ->
                try
                    let nudgeFn = Dyn.get helpers "nudge"
                    let! _ = (Dyn.call2 nudgeFn request.workspaceId prompt :?> JS.Promise<bool>) |> Async.AwaitPromise
                    return rememberAction state request.workspaceId action
                with _ ->
                    coordinator.Value <- clearRuntimeSession coordinator.Value request.workspaceId
                    return state
    }

let private handleEvent (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
                          (state: StreamEndState) (coordinator: CoordinatorRuntimeState ref)
                          (event: obj) (helpers: obj) : JS.Promise<StreamEndState> =
    async {
        match parseHookEvent event with
        | Ignore -> return state
        | StreamEnd properties ->
            let decoded = decodeHookEvent event
            if Dyn.isNullish helpers || Set.contains decoded.workspaceId state.stoppedWorkspaces then return state
            elif decoded.stopReason = "queued-message" then return state
            else
                let lastMessage = getLastAssistantText properties
                let! todos = tryGetTodos helpers decoded.workspaceId
                let request = buildNudgeRequest reviewStore state decoded.workspaceId todos lastMessage
                return! handleNudgeRequest state coordinator helpers request
        | StreamAbort workspaceId ->
            reviewStore.deactivateReview workspaceId
            coordinator.Value <- clearRuntimeSession coordinator.Value workspaceId
            return clearWorkspaceState state workspaceId
        | AbortedError workspaceId ->
            return suppressWorkspace state coordinator workspaceId
    } |> Async.StartAsPromise

let createEventHook (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) : obj =
    let mutable state = createStreamEndState ()
    let coordinator = ref freshCoordinatorRuntime
    let fn = System.Func<obj, obj, JS.Promise<unit>>(fun event helpers ->
        async {
            let! nextState = handleEvent reviewStore state coordinator event helpers |> Async.AwaitPromise
            state <- nextState
        } |> Async.StartAsPromise)
    box fn
