module Wanxiangshu.Runtime.Messaging.OpencodeSessionEventCodec

open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.TodoStatus

module Dyn = Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeSessionPromptCodec
module OpencodeHostEvent = Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
open Wanxiangshu.Runtime.OpencodeSessionPromptBuilder
open Wanxiangshu.Kernel.Fallback.Continuation

/// Host wire → typed nudge state transition boundary.
///
/// Every Opencode session event that the nudge state machine needs to observe
/// enters the kernel as `NudgeHostEvent`. This module owns the *only* place
/// where the `obj` payload from `OpencodeClientCodec.event` is decomposed into
/// finite DU constructors. Downstream `NudgeState.handleEvent` never re-parses
/// raw part types or abort flags — the host adapter collapses the boolean pair
/// into the three legal `MessageOutcome` cases at this seam.
///
/// PromptInput metadata is a probe only — durable correlation is HostUserMessageId
/// bound at chat.message, with assistant attribution via parentID equality.
/// Re-export shared session event decoders for single-import convenience.
let getSessionID eventType props = OpencodeHostEvent.getSessionID eventType props
let getPartsText parts = OpencodeHostEvent.getPartsText parts

let isCompletedAssistantMessage info = OpencodeHostEvent.isCompletedAssistantMessage info

/// Strict assistant attribution (SPEC §七 step 5 / F-02):
/// parentID must equal the HostUserMessageId bound at chat.message acceptance.
let assistantMatchesHostUserMessage (parentID: string) (hostUserMessageId: string) : bool =
    parentID <> "" && hostUserMessageId <> "" && parentID = hostUserMessageId

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

        let lastIsSynthetic =
            if messagesArr.Length > 0 then
                let last = messagesArr.[messagesArr.Length - 1]
                let info = Dyn.get last "info"
                let role = Dyn.str info "role"
                let agent = Dyn.str info "agent"
                role = "assistant" && isSyntheticAssistantAgent agent
            else
                false

        if lastIsSynthetic then
            true
        else
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
