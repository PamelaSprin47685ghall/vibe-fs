module Wanxiangshu.Shell.OpencodeSessionEventCodec

open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.Nudge.SubmitReviewHooks
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeSessionEventCodecCommon
open Wanxiangshu.Shell.OpencodeSessionEventNudge

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

/// Re-export shared session event helpers from Common module for single-import convenience.
let getSessionID = OpencodeSessionEventCodecCommon.getSessionID
let getPartsText = OpencodeSessionEventCodecCommon.getPartsText
let isCompletedAssistantMessage = OpencodeSessionEventCodecCommon.isCompletedAssistantMessage

/// Re-export nudge event decoder from Nudge module for single-import convenience.
let decodeNudgeHostEvent = OpencodeSessionEventNudge.decodeNudgeHostEvent

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

let private submitReviewPartOutput (part: obj) : string =
    let direct = Dyn.get part "output"
    if not (Dyn.isNullish direct) then string direct
    else
        let state = Dyn.get part "state"
        if Dyn.isNullish state then ""
        else string (Dyn.get state "output")

let private messageHasSubmitReviewWipProgress (message: obj) : bool =
    let parts = Dyn.get message "parts"
    if not (Dyn.isArray parts) then false
    else
        (parts :?> obj array)
        |> Array.exists (fun part ->
            let partType = Dyn.str part "type"
            let tool =
                if partType = "tool" then Dyn.str part "tool"
                elif partType = "dynamic-tool" then Dyn.str part "toolName"
                else ""
            (partType = "tool" || partType = "dynamic-tool")
            && isSubmitReviewToolName tool
            && submitReviewPartOutput part |> isSubmitReviewWipProgressOutput)

let private messageIsUserNudgePrompt (message: obj) : bool =
    let info = Dyn.get message "info"
    let role =
        let r = Dyn.str info "role"
        if r <> "" then r else Dyn.str message "role"
    role = "user" && isNudgePrompt (getPartsText (Dyn.get message "parts"))

let private alreadyNudgedAfterIndex (messages: obj array) (idx: int) : bool =
    messages.[idx + 1 ..]
    |> Array.fold
        (fun nudged message ->
            if messageHasSubmitReviewWipProgress message then false
            elif messageIsUserNudgePrompt message then true
            else nudged)
        false

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
            let alreadyNudged = alreadyNudgedAfterIndex messagesArr idx
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
