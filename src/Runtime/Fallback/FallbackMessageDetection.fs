// Based on opencode-auto-resume raw-tool-call detection patterns + Wanxiangshu protocol extensions.
module Wanxiangshu.Runtime.Fallback.FallbackMessageDetection

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.FallbackMessageParser
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel

module Dyn = Wanxiangshu.Runtime.Dyn

/// Find the index of the last assistant message.
let lastAssistantIndex (msgs: obj array) : int option =
    if isNull msgs || msgs.Length = 0 then
        None
    else
        msgs
        |> Array.tryFindIndexBack (fun msg -> Dyn.str (Dyn.get msg "info") "role" = "assistant")

/// Try to extract the message id of the most recent assistant message.
let tryGetLastAssistantMessageId (msgs: obj array) : string option =
    match lastAssistantIndex msgs with
    | None -> None
    | Some idx ->
        let id = Dyn.str (Dyn.get msgs.[idx] "info") "id"
        if id = "" then None else Some id

/// Recovery prompt returned when a tool-call-as-text is detected.
/// `RecoverWithPrompt` injects this verbatim into the user turn; the
/// SSOT for the recovery event is the per-session log file, not this string.
let private recoveryPrompt =
    "You produced the tool call as raw text instead of properly dispatching the function calling protocol. I cannot execute it. Please invoke it again using proper structured tool calling protocol instead of putting XML tags inside your thought process or output."

/// Scan messages backwards for the most recent assistant text containing an
/// XML tool call pattern. Returns the recovery prompt when detected.
let scanToolCallAsText (msgs: obj array) : string option =
    if isNull msgs || msgs.Length = 0 then
        None
    else
        let lastAssistant =
            msgs
            |> Array.rev
            |> Array.tryFind (fun msg -> Dyn.str (Dyn.get msg "info") "role" = "assistant")

        match lastAssistant with
        | None -> None
        | Some msg ->
            let parts = Dyn.get msg "parts"

            if not (Dyn.isArray parts) then
                None
            else
                let hasToolText =
                    (parts :?> obj array)
                    |> Array.rev
                    |> Array.exists (fun part ->
                        Dyn.str part "type" = "text" && containsToolCallAsText (Dyn.str part "text"))

                if hasToolText then Some recoveryPrompt else None

/// Scan messages backwards for the most recent task/todowrite tool part and
/// report whether every todo item is in a terminal status.
let allTodosCompleted (msgs: obj array) : bool =
    if isNull msgs || msgs.Length = 0 then
        false
    else
        msgs
        |> Array.rev
        |> Array.tryPick (fun msg ->
            let parts = Dyn.get msg "parts"

            if not (Dyn.isArray parts) then
                None
            else
                (parts :?> obj array)
                |> Array.rev
                |> Array.tryPick (fun part ->
                    let pt, tl = Dyn.str part "type", Dyn.str part "tool"

                    if (pt = "tool" || pt = "dynamic-tool") && (tl = "task" || tl = "todowrite") then
                        let todos = Dyn.get (Dyn.get (Dyn.get part "state") "input") "todos"

                        if Dyn.isArray todos then
                            (todos :?> obj array)
                            |> Array.forall (fun t ->
                                TodoItemStatus.isTerminal (TodoItemStatus.fromString (Dyn.str t "status")))
                            |> Some
                        else
                            None
                    else
                        None))
        |> Option.defaultValue false

/// Detect whether the last assistant message carries no tool calls and no
/// visible text content — i.e. the LLM returned an empty output.
let isIdleNoContentAndNoTools (msgs: obj array) : bool =
    match lastAssistantIndex msgs with
    | None -> false
    | Some idx ->
        let msg = msgs.[idx]
        let parts = Dyn.get msg "parts"

        if isNull parts || Dyn.isNullish parts || not (Dyn.isArray parts) then
            true
        else
            let arr = parts :?> obj array

            let hasTool =
                arr
                |> Array.exists (fun p -> let pt = Dyn.str p "type" in pt = "tool" || pt = "dynamic-tool")

            if hasTool then
                false
            else
                not (
                    arr
                    |> Array.exists (fun p ->
                        Dyn.str p "type" = "text"
                        && not (System.String.IsNullOrWhiteSpace(Dyn.str p "text")))
                )

/// Check if the last assistant message shows it was aborted by the user.
/// Returns Some ErrorInput if aborted, otherwise None.
let tryGetLastAssistantAbortInfo (msgs: obj array) : ErrorInput option =
    match lastAssistantIndex msgs with
    | None -> None
    | Some idx ->
        let msg = msgs.[idx]
        let info = Dyn.get msg "info"

        if isNull info || Dyn.isNullish info then
            None
        else
            let f, err = Dyn.str info "finish", Dyn.get info "error"
            let reason = FinishReason.fromString f

            let isAbort =
                FinishReason.isAbort reason
                || (not (isNull err)
                    && (Dyn.str err "name" = "MessageAbortedError" || Dyn.str err "name" = "AbortError"))

            if isAbort then
                Some
                    { ErrorName = "MessageAbortedError"
                      DomainError = Some MessageAborted
                      Message = "Generation aborted before producing content"
                      StatusCode = None
                      IsRetryable = Some false }
            else
                None

/// Check if the last assistant message finished because of a tool call.
let isLastAssistantToolFinish (msgs: obj array) : bool =
    match lastAssistantIndex msgs with
    | None -> false
    | Some idx ->
        let info = Dyn.get msgs.[idx] "info"
        let finish = Dyn.str info "finish"
        let reason = FinishReason.fromString finish

        match reason with
        | FinishReason.ToolCalls -> true
        | FinishReason.Unknown s ->
            let lower = s.ToLowerInvariant()
            lower.Contains("tool") && lower <> "tool_use_error"
        | _ -> false

/// Check if there is any tool result message after the last assistant turn.
let hasToolResultAfter (msgs: obj array) : bool =
    match lastAssistantIndex msgs with
    | None -> false
    | Some idx ->
        if idx + 1 >= msgs.Length then
            false
        else
            msgs.[idx + 1 ..]
            |> Array.exists (fun msg ->
                let mInfo = Dyn.get msg "info"
                let mRole = Dyn.str mInfo "role"
                isToolResultRoleString mRole)
