// Based on opencode-auto-resume raw-tool-call detection patterns + Wanxiangshu protocol extensions.
module Wanxiangshu.Shell.FallbackMessageCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackMessageParser
open Wanxiangshu.Kernel.Messaging

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
                                let s = Dyn.str t "status" in s = "completed" || s = "cancelled")
                            |> Some
                        else
                            None
                    else
                        None))
        |> Option.defaultValue false

/// Detect whether the last assistant message carries no tool calls and no
/// visible text content — i.e. the LLM returned an empty output.
let isIdleNoContentAndNoTools (msgs: obj array) : bool =
    if isNull msgs || msgs.Length = 0 then
        false
    else
        msgs
        |> Array.rev
        |> Array.tryPick (fun msg ->
            if Dyn.str (Dyn.get msg "info") "role" <> "assistant" then
                None
            else
                let parts = Dyn.get msg "parts"

                if not (Dyn.isArray parts) then
                    None
                else
                    let arr = parts :?> obj array

                    let hasTool =
                        arr
                        |> Array.exists (fun p -> let pt = Dyn.str p "type" in pt = "tool" || pt = "dynamic-tool")

                    if hasTool then
                        None
                    else
                        Some(
                            not (
                                arr
                                |> Array.exists (fun p ->
                                    Dyn.str p "type" = "text"
                                    && not (System.String.IsNullOrWhiteSpace(Dyn.str p "text")))
                            )
                        ))
        |> Option.defaultValue false

/// Check if the last assistant message shows it was aborted by the user.
/// Returns Some ErrorInput if aborted, otherwise None.
let tryGetLastAssistantAbortInfo (msgs: obj array) : ErrorInput option =
    if isNull msgs || msgs.Length = 0 then
        None
    else
        msgs
        |> Array.rev
        |> Array.tryPick (fun msg ->
            let info = Dyn.get msg "info"

            if isNull info || Dyn.str info "role" <> "assistant" then
                None
            else
                let f, err = Dyn.str info "finish", Dyn.get info "error"

                let isAbort =
                    f = "abort"
                    || f = "interrupted"
                    || f = "cancelled"
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
                    None)

/// Decode a FallbackModel from a raw host object (which can be a string like "openai/gpt-5" or an object).
let decodeModelFromObj (modelObj: obj) : FallbackModel option =
    if isNull modelObj || Dyn.isNullish modelObj then
        None
    elif Dyn.typeIs modelObj "string" then
        let s = string modelObj
        let slash = s.IndexOf('/')

        if slash <= 0 || slash >= s.Length - 1 then
            None
        else
            Some
                { ProviderID = s.[0 .. slash - 1].Trim()
                  ModelID = s.[slash + 1 ..].Trim()
                  Variant = None
                  Temperature = None
                  TopP = None
                  MaxTokens = None
                  ReasoningEffort = None
                  Thinking = false }
    else
        let getStr k k2 =
            let v = Dyn.str modelObj k in if v <> "" then v else Dyn.str modelObj k2

        let provider = getStr "providerID" "provider"

        let modelId =
            let v = Dyn.str modelObj "modelID"
            let v = if v <> "" then v else Dyn.str modelObj "id"
            if v <> "" then v else Dyn.str modelObj "model"

        if provider <> "" && modelId <> "" then
            Some
                { ProviderID = provider
                  ModelID = modelId
                  Variant = None
                  Temperature = None
                  TopP = None
                  MaxTokens = None
                  ReasoningEffort = None
                  Thinking = false }
        else
            None

/// Find the index of the last assistant message.
let lastAssistantIndex (msgs: obj array) : int option =
    if isNull msgs || msgs.Length = 0 then
        None
    else
        msgs
        |> Array.tryFindIndexBack (fun msg -> Dyn.str (Dyn.get msg "info") "role" = "assistant")

/// Check if the last assistant message finished because of a tool call.
let isLastAssistantToolFinish (msgs: obj array) : bool =
    match lastAssistantIndex msgs with
    | None -> false
    | Some idx ->
        let info = Dyn.get msgs.[idx] "info"
        let finish = Dyn.str info "finish"
        let finishLower = finish.ToLower()
        finishLower.Contains("tool") && finishLower <> "tool_use_error"

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

/// Find the last user message in the message list and extract its specified model if present.
/// Whether the user message was injected by the fallback runtime is now a
/// per-session log question, not a text-sniffing one: callers should compose
/// `tryGetLatestUserModel` with `fallbackRuntime.GetInjectedModel` when they
/// need the most authoritative "what model should this turn be routed to"
/// answer (see `ResolveLatestModel.resolve`).
let tryGetLatestUserModel (msgs: obj array) : FallbackModel option =
    if isNull msgs || msgs.Length = 0 then
        None
    else
        msgs
        |> Array.rev
        |> Array.tryPick (fun msg ->
            let info = Dyn.get msg "info"

            if isNull info || Dyn.isNullish info then
                None
            else
                let role = Dyn.str info "role"

                if role <> "user" then
                    None
                else
                    let modelObj = Dyn.get info "model"
                    decodeModelFromObj modelObj)
