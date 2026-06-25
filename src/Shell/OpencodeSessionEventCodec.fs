module VibeFs.Shell.OpencodeSessionEventCodec

open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.NudgeState
open VibeFs.Shell.Dyn
open VibeFs.Shell.ErrorClassify

/// Host wire → typed nudge state transition boundary.
///
/// Every Opencode session event that the nudge state machine needs to observe
/// enters the kernel as `NudgeHostEvent`. This module owns the *only* place
/// where the `obj` payload from `OpencodeClientCodec.event` is decomposed into
/// finite DU constructors. Downstream `NudgeState.handleEvent` never re-parses
/// raw part types or abort flags — the host adapter collapses the boolean pair
/// into the three legal `MessageOutcome` cases at this seam.
///
/// All helpers here are pure `obj → typed` decoders. Encoding back to host
/// payloads (e.g. `createPromptBody`) lives next to the decoders so the wire
/// format stays a single read/write site.
let private sessionEventTypes =
    Set.ofList [
        "session.created"
        "session.updated"
        "session.deleted"
        "session.delete"
        "session.close"
        "session.remove"
    ]

/// Resolve the sessionID of a session event. Different event shapes stash the
/// id in different locations — fall back through the known carriers in
/// priority order, and only fall through to `info.id` for the lifecycle-style
/// events that carry the id directly on `info`.
let getSessionID (eventType: string) (props: obj) : string =
    let part = Dyn.get props "part"
    let info = Dyn.get props "info"
    let candidates =
        [ Dyn.str props "sessionID"
          Dyn.str part "sessionID"
          Dyn.str info "sessionID"
          if Set.contains eventType sessionEventTypes then
              Dyn.str info "id"
          else "" ]
    candidates |> List.tryFind (fun s -> s <> "") |> Option.defaultValue ""

/// Concatenate text content from an Opencode `parts` array. Non-array or
/// non-text parts are silently skipped so callers can pass either a typed
/// list or a missing payload without a separate guard.
let getPartsText (parts: obj) : string =
    if not (Dyn.isArray parts) then ""
    else
        (parts :?> obj array)
        |> Array.choose (fun part ->
            if Dyn.str part "type" = "text" then
                let text = Dyn.get part "text"
                if Dyn.isNullish text then None else Some (string text)
            else None)
        |> String.concat "\n"

/// Test whether `info` represents a completed assistant message. An assistant
/// message counts as completed when its role is assistant AND it carries a
/// terminal finish OR a numeric `time.completed`. An error-bearing message is
/// never considered completed (aborts are surfaced separately).
let isCompletedAssistantMessage (info: obj) : bool =
    if Dyn.isNullish info then false
    else
        let isAssistant = Dyn.str info "role" = "assistant" || Dyn.str info "type" = "assistant"
        let hasError = not (Dyn.isNullish (Dyn.get info "error"))
        if not isAssistant || hasError then false
        else
            let finishVal = Dyn.get info "finish"
            if not (Dyn.isNullish finishVal) && Dyn.typeIs finishVal "string" then
                isTerminalAssistantFinish (string finishVal)
            else
                let timeCompleted = Dyn.get (Dyn.get info "time") "completed"
                not (Dyn.isNullish timeCompleted) && Dyn.typeIs timeCompleted "number"

/// Decode a todo list payload into the *open* todo contents, dropping items
/// with terminal status. The returned strings are the raw `content` strings
/// (callers compare to nudge prompts); status filter happens here.
let decodeTodos (todosData: obj) : string list =
    if Dyn.isArray todosData then
        (todosData :?> obj array)
        |> Array.choose (fun todo ->
            let status = Dyn.str todo "status"
            let content = Dyn.str todo "content"
            if content = "" then None
            else
                match todoStatusOfString status with
                | Some s when isTerminal s -> None
                | _ -> Some content)
        |> Array.toList
    else []

/// Fallback open-todo recovery by walking `messagesData` backwards for a `task`
/// tool part whose `input.todos` survives in history. Used only when the live
/// todo API is unavailable or returns an empty list — the messages fallback is
/// the historical record, not the authority.
let recoverOpenTodosFromMessages (messagesData: obj) : string list =
    if not (Dyn.isArray messagesData) then []
    else
        (messagesData :?> obj array)
        |> Array.rev
        |> Array.tryPick (fun message ->
            let parts = Dyn.get message "parts"
            if not (Dyn.isArray parts) then None
            else
                (parts :?> obj array)
                |> Array.rev
                |> Array.tryPick (fun part ->
                    if Dyn.str part "type" <> "tool" || Dyn.str part "tool" <> "task" then None
                    else
                        let state = Dyn.get part "state"
                        let input = Dyn.get state "input"
                        let todos = Dyn.get input "todos"
                        if not (Dyn.isArray todos) then None
                        else
                            let openItems =
                                (todos :?> obj array)
                                |> Array.choose (fun todo ->
                                    let content = Dyn.str todo "content"
                                    let status = Dyn.str todo "status"
                                    if content = "" || status = "" then None
                                    else
                                        match todoStatusOfString status with
                                        | Some s when isTerminal s -> None
                                        | _ -> Some content)
                            Some openItems))
        |> Option.defaultValue [||]
        |> Array.toList

/// Locate the last completed assistant message in `messagesData` and report
/// `(text, agent, alreadyNudged)`. `alreadyNudged` is true iff a nudge prompt
/// already trails the assistant turn — used to suppress double-nudges that
/// would otherwise survive a process restart.
let decodeLastAssistant (messagesData: obj) : string * string option * bool =
    if Dyn.isArray messagesData then
        let messagesArr = messagesData :?> obj array
        let lastAssistantIdx =
            messagesArr
            |> Array.tryFindIndexBack (fun msg ->
                let info = Dyn.get msg "info"
                isCompletedAssistantMessage info
                && not (isSyntheticAssistantAgent (Dyn.str info "agent")))
        match lastAssistantIdx with
        | Some idx ->
            let msg = messagesArr.[idx]
            let info = Dyn.get msg "info"
            let agentVal = Dyn.get info "agent"
            let agent = if Dyn.isNullish agentVal then None else Some (string agentVal)
            let text = getPartsText (Dyn.get msg "parts")
            let alreadyNudged =
                messagesArr.[idx + 1 ..]
                |> Array.exists (fun m -> isNudgePrompt (getPartsText (Dyn.get m "parts")))
            text, agent, alreadyNudged
        | None -> "", None, false
    else "", None, false

/// Build the host wire prompt body for `session.prompt`. The agent-scoped
/// variant carries an `agent` field so the host routes to the same assistant
/// that produced the last turn; without an agent the host falls back to the
/// session default.
let createPromptBody (agent: string option) (text: string) : obj =
    match agent with
    | Some a -> box {| agent = a; parts = [| box {| ``type`` = "text"; text = text |} |] |}
    | None -> box {| parts = [| box {| ``type`` = "text"; text = text |} |] |}

let private pureMap =
    Map [
        "stream-abort", StreamAbort
        "session.delete", SessionDeleted
        "session.close", SessionDeleted
        "session.remove", SessionDeleted
        "session.deleted", SessionDeleted
        "session.next.retried", SessionNextRetried
        "session.idle", SessionIdle
    ]

let private extractorMap : Map<string, obj -> NudgeHostEvent option> =
    Map [
        "session.next.prompted", fun props ->
            let prompt = Dyn.get props "prompt"
            let promptText = Dyn.str prompt "text"
            let text =
                if promptText <> "" then promptText
                else
                    let partsText = getPartsText (Dyn.get props "parts")
                    if partsText <> "" then partsText else Dyn.str props "text"
            Some (SessionNextPrompted text)

        "message.updated", fun props ->
            let info = Dyn.get props "info"
            let outcome =
                if isAbortDomainError (Dyn.get info "error") then UpdateAborted
                elif isCompletedAssistantMessage info then UpdateCompletedAssistant
                else UpdateNoChange
            Some (MessageUpdated outcome)

        "message.part.updated", fun props ->
            let part = Dyn.get props "part"
            let partType = Dyn.str part "type"
            let outcome =
                if partType = "retry" then PartRetry
                elif isAbortDomainError (Dyn.get part "error")
                  || isAbortDomainError (Dyn.get part "state") then PartAborted
                elif isRetryProgressPart partType then PartRetryProgress
                else PartOther
            Some (MessagePartUpdated outcome)

        "session.next.step.failed", fun props ->
            Some (SessionNextStepFailed (if isAbortDomainError (Dyn.get props "error") then StepFailAbort else StepFailOther))

        "session.next.tool.failed", fun props ->
            Some (SessionNextToolFailed (if isAbortDomainError (Dyn.get props "error") then ToolFailAbort else ToolFailOther))

        "session.next.step.ended", fun props ->
            let direct = Dyn.str props "finish"
            let finish = if direct <> "" then direct else Dyn.str (Dyn.get props "info") "finish"
            Some (SessionNextStepEnded finish)

        "session.error", fun props ->
            Some (SessionError (if isAbortDomainError (Dyn.get props "error") then SessionErrorAbort else SessionErrorOther))

        "session.status", fun props ->
            let ev =
                match Dyn.str (Dyn.get props "status") "type" with
                | "idle" -> SessionStatusIdle
                | "busy" -> SessionStatusBusy
                | "retry" -> SessionStatusRetry
                | _ -> Other
            Some ev
    ]

/// Decode any Opencode session event into the finite `NudgeHostEvent` DU. Pure
/// events short-circuit via `pureMap`; events whose outcome depends on the
/// payload use the per-type extractors. Unknown event types collapse to `Other`
/// — never throws, so `NudgeState.handleEvent` cannot be poisoned by a
/// malformed event payload.
let decodeNudgeHostEvent (eventType: string) (props: obj) : NudgeHostEvent =
    match Map.tryFind eventType pureMap with
    | Some ev -> ev
    | None ->
        match Map.tryFind eventType extractorMap with
        | Some extract -> extract props |> Option.defaultValue Other
        | None -> if isRetryProgressEvent eventType then RetryProgress else Other