module VibeFs.Shell.NudgeRuntime

open Fable.Core
open VibeFs.Shell.Dyn
open VibeFs.Kernel
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.NudgeState

type NudgeRuntimeEvent =
    | Ignore
    | StreamEnd of workspaceId: string * stopReason: string * lastAssistantMessage: string
    | StreamAbort of workspaceId: string
    | AbortedError of workspaceId: string

type private StateHolder<'state>(initialState: 'state) =
    let mutable state = initialState

    member _.Mutate<'result>(transition: 'state -> 'state * 'result) : 'result =
        let nextState, result = transition state
        state <- nextState
        result

let private tryGetTodos (helpers: obj) (workspaceId: string) : JS.Promise<string list> =
    promise {
        try
            let getTodosFn = Dyn.get helpers "getTodos"
            let! result = unbox<JS.Promise<obj>> (Dyn.call1 getTodosFn workspaceId)
            if Dyn.isArray result then
                return (result :?> obj array) |> Array.map string |> List.ofArray
            else
                return []
        with ex ->
            return []
    }

let private tryGetChatHistory
    (getChatHistory: (string -> JS.Promise<obj array>) option)
    (workspaceId: string)
    : JS.Promise<obj array> =
    promise {
        match getChatHistory with
        | None -> return [||]
        | Some getHistory ->
            try
                return! getHistory workspaceId
            with ex ->
                return [||]
    }

let private getPartsText (parts: obj) : string =
    if not (Dyn.isArray parts) then ""
    else
        (parts :?> obj array)
        |> Array.choose (fun part ->
            if Dyn.str part "type" = "text" then
                let text = Dyn.get part "text"
                if Dyn.isNullish text then None else Some (string text)
            else None)
        |> String.concat "\n"

let private messageHasSubmitReviewWipProgress (message: obj) : bool =
    let parts = Dyn.get message "parts"
    if not (Dyn.isArray parts) then false
    else
        (parts :?> obj array)
        |> Array.exists (fun part ->
            Dyn.str part "type" = "dynamic-tool"
            && isSubmitReviewToolName (Dyn.str part "toolName")
            && (let direct = Dyn.get part "output"
                if not (Dyn.isNullish direct) then string direct
                else
                    let state = Dyn.get part "state"
                    if Dyn.isNullish state || Dyn.typeIs state "string" then ""
                    else string (Dyn.get state "output"))
               |> isSubmitReviewWipProgressOutput)

let private messageIsUserNudgePrompt (message: obj) : bool =
    Dyn.str message "role" = "user" && isNudgePrompt (getPartsText (Dyn.get message "parts"))

let private alreadyNudgedAfterIndex (messages: obj array) (index: int) : bool =
    messages.[index + 1 ..]
    |> Array.fold
        (fun nudged message ->
            if messageHasSubmitReviewWipProgress message then false
            elif messageIsUserNudgePrompt message then true
            else nudged)
        false

let private decodeLastAssistant (messages: obj array) : string * bool =
    let lastAssistantIndex =
        messages
        |> Array.tryFindIndexBack (fun message ->
            Dyn.str message "role" = "assistant"
            && not (isSyntheticAssistantAgent (Dyn.str message "agent")))

    match lastAssistantIndex with
    | None -> "", false
    | Some index ->
        let text = getPartsText (Dyn.get messages.[index] "parts")
        let alreadyNudged = alreadyNudgedAfterIndex messages index
        text, alreadyNudged

let private collectSnapshot
    (getChatHistory: (string -> JS.Promise<obj array>) option)
    (helpers: obj)
    (workspaceId: string)
    (eventLastAssistantMessage: string)
    : JS.Promise<SessionSnapshot> =
    promise {
        let! todos = tryGetTodos helpers workspaceId
        let! history = tryGetChatHistory getChatHistory workspaceId
        let historyLastAssistantMessage, historyAlreadyNudged = decodeLastAssistant history
        let effectiveLastAssistantMessage, alreadyNudged =
            if historyLastAssistantMessage = "" then
                eventLastAssistantMessage, false
            elif eventLastAssistantMessage <> "" && eventLastAssistantMessage <> historyLastAssistantMessage then
                eventLastAssistantMessage, false
            else
                historyLastAssistantMessage, historyAlreadyNudged

        return
            { todos = todos
              lastAssistantMessage = effectiveLastAssistantMessage
              alreadyNudged = alreadyNudged
              agentFromMessage = None }
    }

let private runNudgeFlow
    (holder: StateHolder<NudgeShellState>)
    (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
    (getChatHistory: (string -> JS.Promise<obj array>) option)
    (helpers: obj)
    (workspaceId: string)
    (lastAssistantMessage: string)
    : JS.Promise<unit> =
    promise {
        try
            let! snapshot = collectSnapshot getChatHistory helpers workspaceId lastAssistantMessage
            match holder.Mutate(fun state -> decideNudge reviewStore.isReviewActive (fun _ -> None) state workspaceId snapshot) with
            | StandDown -> ()
            | Send(promptText, _) ->
                let! delivered =
                    promise {
                        try
                            let nudgeFn = Dyn.get helpers "nudge"
                            return! unbox<JS.Promise<bool>> (Dyn.call2 nudgeFn workspaceId promptText)
                        with ex ->
                            return false
                    }

                let outcome = if delivered then Delivered else Busy
                holder.Mutate(fun state ->
                    match tryRecordSend state workspaceId outcome with
                    | Some nextState -> nextState, ()
                    | None -> state, ())
        with ex ->
            holder.Mutate(fun state -> clearSession state workspaceId, ())
    }

let private startNudgeFlow
    (holder: StateHolder<NudgeShellState>)
    (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
    (getChatHistory: (string -> JS.Promise<obj array>) option)
    (helpers: obj)
    (workspaceId: string)
    (lastAssistantMessage: string)
    : unit =
    runNudgeFlow holder reviewStore getChatHistory helpers workspaceId lastAssistantMessage |> Promise.start

type NudgeRuntime
    (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore,
     getChatHistory: (string -> JS.Promise<obj array>) option) =

    let holder = StateHolder<NudgeShellState>(emptyState)

    member _.HandleEvent(parsed: NudgeRuntimeEvent, helpers: obj) : JS.Promise<unit> =
        promise {
            match parsed with
            | Ignore -> return ()
            | StreamEnd(workspaceId, stopReason, lastAssistantMessage) ->
                if Dyn.isNullish helpers || stopReason = "queued-message" then
                    return ()
                else
                    // Dedup is delegated to Kernel.decideNudge via snapshot.alreadyNudged
                    // (history tail is the single source of truth). No in-process gate here:
                    // a repeat stream-end for the same stop yields StandDown from decideNudge
                    // without firing a second send.
                    startNudgeFlow holder reviewStore getChatHistory helpers workspaceId lastAssistantMessage
                    return ()
            | StreamAbort workspaceId
            | AbortedError workspaceId ->
                holder.Mutate(fun state -> clearSession state workspaceId, ())
                return ()
        }

let createNudgeRuntime
    (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
    (getChatHistory: (string -> JS.Promise<obj array>) option)
    : NudgeRuntime =
    NudgeRuntime(reviewStore, getChatHistory)
