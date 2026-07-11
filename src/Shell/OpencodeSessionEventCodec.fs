module Wanxiangshu.Shell.OpencodeSessionEventCodec

open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.TodoStatus

open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeSessionPromptCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodecCommon

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

let isCompletedAssistantMessage =
    OpencodeSessionEventCodecCommon.isCompletedAssistantMessage

/// Decode a todo list payload into the *open* todo contents, dropping items
/// with terminal status. The returned strings are the raw `content` strings
/// (callers compare to nudge prompts); status filter happens here.
let decodeTodos (todosData: obj) : string list =
    if Dyn.isArray todosData then
        (todosData :?> obj array)
        |> Array.choose (fun todo ->
            let status = Dyn.str todo "status"
            let content = Dyn.str todo "content"

            if content = "" then
                None
            else
                match todoStatusOfString status with
                | Some s when isTerminal s -> None
                | _ -> Some content)
        |> Array.toList
    else
        []

/// Fallback open-todo recovery by walking `messagesData` backwards for a `task`
/// tool part whose `input.todos` survives in history. Used only when the live
/// todo API is unavailable or returns an empty list — the messages fallback is
/// the historical record, not the authority.
let recoverOpenTodosFromMessages (messagesData: obj) : string list =
    if not (Dyn.isArray messagesData) then
        []
    else
        (messagesData :?> obj array)
        |> Array.rev
        |> Array.tryPick (fun message ->
            let parts = Dyn.get message "parts"

            if not (Dyn.isArray parts) then
                None
            else
                (parts :?> obj array)
                |> Array.rev
                |> Array.tryPick (fun part ->
                    if Dyn.str part "type" <> "tool" || Dyn.str part "tool" <> "task" then
                        None
                    else
                        let state = Dyn.get part "state"
                        let input = Dyn.get state "input"
                        let todos = Dyn.get input "todos"

                        if not (Dyn.isArray todos) then
                            None
                        else
                            let openItems =
                                (todos :?> obj array)
                                |> Array.choose (fun todo ->
                                    let content = Dyn.str todo "content"
                                    let status = Dyn.str todo "status"

                                    if content = "" || status = "" then
                                        None
                                    else
                                        match todoStatusOfString status with
                                        | Some s when isTerminal s -> None
                                        | _ -> Some content)

                            Some openItems))
        |> Option.defaultValue [||]
        |> Array.toList

/// Try the strict completed-assistant predicate first, then fall back to the
/// last assistant message that has no error and is not a synthetic agent.
let tryFindLastAssistantIdx (messagesArr: obj array) : int option =
    let strict =
        messagesArr
        |> Array.tryFindIndexBack (fun msg ->
            let info = Dyn.get msg "info"

            isCompletedAssistantMessage info
            && not (isSyntheticAssistantAgent (Dyn.str info "agent")))

    match strict with
    | Some _ -> strict
    | None ->
        messagesArr
        |> Array.tryFindIndexBack (fun msg ->
            let info = Dyn.get msg "info"

            let isAssistant =
                Dyn.str info "role" = "assistant" || Dyn.str info "type" = "assistant"

            let hasError = not (Dyn.isNullish (Dyn.get info "error"))

            isAssistant
            && not hasError
            && not (isSyntheticAssistantAgent (Dyn.str info "agent")))

/// Locate the last completed assistant message in `messagesData` → `(text, agent)`.
let decodeLastAssistant (messagesData: obj) : string * string option =
    if Dyn.isArray messagesData then
        let messagesArr = messagesData :?> obj array

        match tryFindLastAssistantIdx messagesArr with
        | Some idx ->
            let msg = messagesArr.[idx]
            let info = Dyn.get msg "info"
            let agentVal = Dyn.get info "agent"

            let agent =
                if Dyn.isNullish agentVal then
                    None
                else
                    Some(string agentVal)

            let text = getPartsText (Dyn.get msg "parts")
            text, agent
        | None -> "", None
    else
        "", None

/// Check if nudge should be skipped based on message history.
let shouldSkipNudge (messagesData: obj) : bool =
    if not (Dyn.isArray messagesData) then
        false
    else
        let messagesArr = messagesData :?> obj array

        let absoluteLastAssistantIdx =
            messagesArr
            |> Array.tryFindIndexBack (fun msg ->
                let info = Dyn.get msg "info"
                let role = Dyn.str info "role"
                role = "assistant" && not (isSyntheticAssistantAgent (Dyn.str info "agent")))

        match absoluteLastAssistantIdx with
        | Some idx ->
            let info = Dyn.get (messagesArr.[idx]) "info"
            let finish = Dyn.str info "finish"
            let reason = FinishReason.fromString finish

            if FinishReason.isAbort reason then
                true
            else
                let isToolFinish = FinishReason.isToolFinish reason

                if not isToolFinish then
                    false
                else
                    let hasToolResultAfter =
                        messagesArr.[idx + 1 ..]
                        |> Array.exists (fun msg ->
                            let mInfo = Dyn.get msg "info"
                            let mRole = Dyn.str mInfo "role"
                            isToolResultRoleString mRole)

                    not hasToolResultAfter
        | None -> false

/// Build the host wire prompt body for `session.prompt`. The agent-scoped
/// variant carries an `agent` field so the host routes to the same assistant
/// that produced the last turn; without an agent the host falls back to the
/// session default.
let createPromptBody (agent: string option) (text: string) : obj =
    match agent with
    | Some a ->
        box
            {| agent = a
               parts = [| box {| ``type`` = "text"; text = text |} |] |}
    | None -> box {| parts = [| box {| ``type`` = "text"; text = text |} |] |}

/// Build prompt body with optional agent and model override.
/// Build prompt body with optional agent and model override.
let createPromptBodyWithModel (agent: string option) (model: string option) (text: string) : obj =
    let textPart = box {| ``type`` = "text"; text = text |}
    let parts: obj array = [| textPart |]

    let baseBody =
        match agent with
        | Some a -> box {| agent = a; parts = parts |}
        | None -> box {| parts = parts |}

    match model |> Option.bind tryDecodePromptModelFromModelString with
    | Some promptModel -> Dyn.withKey baseBody "model" promptModel
    | None -> baseBody
