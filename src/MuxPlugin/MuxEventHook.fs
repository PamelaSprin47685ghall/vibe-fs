module VibeFs.MuxPlugin.MuxEventHook

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.Prompts
open VibeFs.Mux.StreamEnd

/// Extract the last assistant text from event properties.parts.
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

/// The inner async logic for the event hook.
let private handleEvent (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore)
                         (state: StreamEndState) (coordinator: NudgeCoordinator)
                         (event: obj) (helpers: obj) : JS.Promise<unit> =
    async {
        let eventType = Dyn.str event "type"
        let workspaceId = Dyn.str event "workspaceId"
        if workspaceId = "" then () else
            match eventType with
            | "stream-end" ->
                let properties = Dyn.get event "properties"
                let metadata = Dyn.get properties "metadata"
                let stopReason = Dyn.str metadata "muxStopReason"
                if stopReason = "queued-message" then ()
                elif Dyn.isNullish helpers then ()
                else
                    let lastMessage = getLastAssistantText properties
                    coordinator.clearSession(workspaceId)
                    if state.stoppedWorkspaces.Contains(workspaceId) then ()
                    else
                        let! todosOpt =
                            async {
                                try
                                    let getTodosFn = Dyn.get helpers "getTodos"
                                    let! result = (Dyn.call1 getTodosFn workspaceId :?> JS.Promise<obj array>) |> Async.AwaitPromise
                                    return Some result
                                with _ -> return None
                            }
                        let todos = (defaultArg todosOpt [||]) |> Array.map string |> List.ofArray
                        let action =
                            coordinator.shouldNudge(workspaceId,
                                { todos = todos; lastAssistantMessage = lastMessage
                                  hasActiveRunner = false; isLoopActive = reviewStore.isReviewActive workspaceId })
                        match selectNudgePrompt action todoNudgePrompt loopNudgePrompt with
                        | None -> ()
                        | Some prompt ->
                            try
                                let nudgeFn = Dyn.get helpers "nudge"
                                let! _ = (Dyn.call2 nudgeFn workspaceId prompt :?> JS.Promise<bool>) |> Async.AwaitPromise
                                let prev = if state.deliveredCounts.ContainsKey(workspaceId) then state.deliveredCounts.[workspaceId] else 0
                                state.deliveredCounts.[workspaceId] <- prev + 1
                            with _ -> coordinator.clearSession(workspaceId)
            | "stream-abort" ->
                reviewStore.deactivateReview workspaceId
                coordinator.clearSession(workspaceId)
                state.stoppedWorkspaces.Add(workspaceId) |> ignore
                state.retryPendingWorkspaces.Remove(workspaceId) |> ignore
            | "error" ->
                let properties = Dyn.get event "properties"
                let errorType = Dyn.str properties "errorType"
                if errorType = "aborted" then
                    coordinator.suppress workspaceId
                    state.stoppedWorkspaces.Add(workspaceId) |> ignore
            | _ -> ()
    } |> Async.StartAsPromise

/// Create the event hook as a proper two-argument JS function.
/// Uses Func<_,_,_> delegate so Fable emits `function(event, helpers) { ... }`
/// instead of a curried `(event) => (helpers) => ...`.
let createEventHook (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore) : obj =
    let state = createStreamEndState ()
    let coordinator = defaultCoordinator
    let fn = System.Func<obj, obj, JS.Promise<unit>>(fun event helpers ->
        handleEvent reviewStore state coordinator event helpers)
    box fn
