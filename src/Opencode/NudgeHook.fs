module VibeFs.Opencode.NudgeHook

open System
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.NudgeState
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.MagicCore
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Opencode.MagicTodo

let private invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> =
    unbox (target?(method)(arg))

let private setOutput (o: obj) (v: string) : unit =
    o?("output") <- v

let private resolvedUnitPromise () : JS.Promise<unit> = Promise.lift ()

// ── Atomic state holder ──
//
// JavaScript is single-threaded, so a synchronous read-modify-write is atomic
// with respect to every other hook callback.  The holder must NEVER await
// inside a transition: that was the freeze.  The old serialized queue chained
// every hook onto one promise, and a transition that awaited `session.prompt`
// (a whole assistant turn) left that promise pending — jamming every later
// chat.message / command.execute.before behind it, and deadlocking outright
// when the nudge's own prompt re-entered chat.message.  All I/O now lives in
// detached flows outside this lock; transitions are pure and instant.

type private StateHolder<'state>(initialState: 'state) =
    let mutable state = initialState

    member _.Mutate<'result>(transition: 'state -> 'state * 'result) : 'result =
        let nextState, result = transition state
        state <- nextState
        result

let private collectSnapshot (client: obj) (sessionID: SessionId) : JS.Promise<SessionSnapshot option> =
    promise {
        try
            let sessionIDStr = Id.sessionIdValue sessionID
            let session = Dyn.get client "session"
            let! todoResp = invoke1 (box {| path = {| id = sessionIDStr |} |}) "todo" session
            let openTodos = decodeTodos (Dyn.get todoResp "data")
            let! (lastAssistantMessage, agentFromMessage, messageCount) =
                promise {
                    try
                        let! messagesResp = invoke1 (box {| path = {| id = sessionIDStr |} |}) "messages" session
                        let text, agent, count = decodeLastAssistant (Dyn.get messagesResp "data")
                        return (text, agent, count)
                    with _ ->
                        return ("", None, None)
                }
            return Some { todos = openTodos
                          lastAssistantMessage = lastAssistantMessage
                          messageCount = messageCount
                          agentFromMessage = agentFromMessage }
        with _ -> return None
    }

let private sendNudge (client: obj) (sessionID: SessionId) (agentOpt: string option) (promptText: string) : JS.Promise<unit> =
    promise {
        let body = createPromptBody agentOpt promptText
        let promptArg = box {| path = box {| id = Id.sessionIdValue sessionID |}; body = body |}
        let session = Dyn.get client "session"
        do! invoke1 promptArg "prompt" session |> Promise.map ignore
    }

let private decodeNudgeHostEvent (eventType: string) (props: obj) : NudgeHostEvent =
    match eventType with
    | "stream-abort" -> StreamAbort
    | "session.delete" | "session.close" | "session.remove" | "session.deleted" -> SessionDeleted
    | "session.next.prompted" ->
        let prompt = Dyn.get props "prompt"
        let promptText = Dyn.str prompt "text"
        let text =
            if promptText <> "" then promptText
            else
                let partsText = getPartsText (Dyn.get props "parts")
                if partsText <> "" then partsText else Dyn.str props "text"
        SessionNextPrompted text
    | "session.next.retried" -> SessionNextRetried
    | "message.updated" ->
        let info = Dyn.get props "info"
        let outcome =
            if isAbortDomainError (Dyn.get info "error") then UpdateAborted
            elif isCompletedAssistantMessage info then UpdateCompletedAssistant
            else UpdateNoChange
        MessageUpdated outcome
    | "message.part.updated" ->
        let part = Dyn.get props "part"
        let partType = Dyn.str part "type"
        let outcome =
            if partType = "retry" then PartRetry
            elif isAbortDomainError (Dyn.get part "error") || isAbortDomainError (Dyn.get part "state") then PartAborted
            elif isRetryProgressPart partType then PartRetryProgress
            else PartOther
        MessagePartUpdated outcome
    | "session.next.step.failed" ->
        SessionNextStepFailed (if isAbortDomainError (Dyn.get props "error") then StepFailAbort else StepFailOther)
    | "session.next.tool.failed" ->
        SessionNextToolFailed (if isAbortDomainError (Dyn.get props "error") then ToolFailAbort else ToolFailOther)
    | "session.next.step.ended" ->
        let direct = Dyn.str props "finish"
        let finish = if direct <> "" then direct else Dyn.str (Dyn.get props "info") "finish"
        SessionNextStepEnded finish
    | "session.idle" -> SessionIdle
    | "session.error" ->
        SessionError (if isAbortDomainError (Dyn.get props "error") then SessionErrorAbort else SessionErrorOther)
    | "session.status" ->
        match Dyn.str (Dyn.get props "status") "type" with
        | "idle" -> SessionStatusIdle
        | "busy" -> SessionStatusBusy
        | "retry" -> SessionStatusRetry
        | _ -> Other
    | _ ->
        if isRetryProgressEvent eventType then RetryProgress else Other

/// The detached nudge flow: all client I/O happens here, never under the lock.
/// Each lock re-entry (`Mutate`) is a pure, instant transition.
let private runNudgeFlow (holder: StateHolder<VibeFs.Kernel.NudgeState.NudgeShellState>) (client: obj)
                          (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
                          (registry: ChildAgentRegistry)
                          (sessionID: SessionId) : JS.Promise<unit> =
    promise {
        try
            let! snapshotOpt = collectSnapshot client sessionID
            match snapshotOpt with
            | None -> holder.Mutate(fun (state: VibeFs.Kernel.NudgeState.NudgeShellState) -> VibeFs.Kernel.NudgeState.clearSession state (Id.sessionIdValue sessionID), ())
            | Some snapshot ->
                let sid = Id.sessionIdValue sessionID
                match holder.Mutate(fun (state: VibeFs.Kernel.NudgeState.NudgeShellState) -> VibeFs.Kernel.NudgeState.decideNudge reviewStore.isReviewActive registry.LookupChildAgent state sid snapshot) with
                | VibeFs.Kernel.NudgeState.StandDown -> ()
                | VibeFs.Kernel.NudgeState.Send(promptText, agentOpt, messageCount) ->
                    let! caught = sendNudge client sessionID agentOpt promptText |> Promise.result
                    let outcome =
                        match caught with
                        | Ok () -> VibeFs.Kernel.NudgeState.Delivered messageCount
                        | Error error ->
                            match translateJsError error with
                            | MessageAborted -> VibeFs.Kernel.NudgeState.Aborted
                            | SessionBusy -> VibeFs.Kernel.NudgeState.Busy
                            | _ -> VibeFs.Kernel.NudgeState.Failed
                    holder.Mutate(fun (state: VibeFs.Kernel.NudgeState.NudgeShellState) ->
                        match VibeFs.Kernel.NudgeState.tryRecordSend state sid outcome with
                        | Some nextState -> nextState, ()
                        | None -> state, ())
        with _ ->
            holder.Mutate(fun (state: VibeFs.Kernel.NudgeState.NudgeShellState) -> VibeFs.Kernel.NudgeState.clearSession state (Id.sessionIdValue sessionID), ())
    }

/// Fire the nudge flow detached from the caller's hook promise.  `Promise.start`
/// kicks off the promise without waiting (JS promises are hot), so the hook
/// returns at once and the rest of the flow — including any `session.prompt` —
/// never blocks the lock.
let private startNudgeFlow (holder: StateHolder<VibeFs.Kernel.NudgeState.NudgeShellState>) (client: obj)
                            (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
                            (registry: ChildAgentRegistry)
                            (sessionID: SessionId) : unit =
    runNudgeFlow holder client reviewStore registry sessionID |> Promise.start

// ── Hook class ──

type NudgeHook(host: Host, ctx: obj, reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore, registry: ChildAgentRegistry) =
    let client () = Dyn.get ctx "client"
    let holder = StateHolder<VibeFs.Kernel.NudgeState.NudgeShellState>(VibeFs.Kernel.NudgeState.emptyState)

    member _.handleChatMessage(sessionID: SessionId, agent: string, parts: obj) : JS.Promise<unit> =
        holder.Mutate(fun state ->
            let text = getPartsText parts
            let sid = Id.sessionIdValue sessionID
            if isNudgePrompt text then state, ()
            else
                let agentOpt = if agent <> "" then Some agent else None
                VibeFs.Kernel.NudgeState.resumeSession (VibeFs.Kernel.NudgeState.rememberAgent state sid agentOpt) sid, ())
        resolvedUnitPromise ()

    member _.handleCommandExecuteBefore(input: obj) (_output: obj) : JS.Promise<unit> =
        let sessionIDStr = Dyn.str input "sessionID"
        holder.Mutate(fun (state: VibeFs.Kernel.NudgeState.NudgeShellState) -> VibeFs.Kernel.NudgeState.resumeSession state sessionIDStr, ())
        resolvedUnitPromise ()

    member _.handleToolExecuteAfter(input: obj) (output: obj) : JS.Promise<unit> =
        promise {
            if normalizeToolName host (Dyn.str input "tool") = "todowrite" then
                let out = Dyn.get output "output"
                if not (Dyn.isNullish out) && Dyn.typeIs out "string" then
                    let s = string out
                    if not (s.Contains meditatorNudge) then
                        setOutput output (s + "\n" + meditatorNudge)
        }

    member _.handleEvent(input: obj) : JS.Promise<unit> =
        let claimed =
            holder.Mutate(fun state ->
                try
                    let event = Dyn.get input "event"
                    let eventType = Dyn.str event "type"
                    let rawProps = Dyn.get event "properties"
                    let props = if Dyn.isNullish rawProps then event else rawProps
                    let sessionIDStr = getSessionID eventType props
                    match Id.trySessionId sessionIDStr with
                    | None -> state, None
                    | Some sessionID ->
                        let nudgeEvent = decodeNudgeHostEvent eventType props
                        let nextState, wantsNudge = NudgeState.handleEvent state (Id.sessionIdValue sessionID) nudgeEvent
                        nextState, (if wantsNudge then Some sessionID else None)
                with _ -> state, None)
        match claimed with
        | Some sessionID -> startNudgeFlow holder (client ()) reviewStore registry sessionID
        | None -> ()
        resolvedUnitPromise ()

let createNudgeHook (host: Host) (ctx: obj) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (registry: ChildAgentRegistry) : NudgeHook =
    NudgeHook(host, ctx, reviewStore, registry)
