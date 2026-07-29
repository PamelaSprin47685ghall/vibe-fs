namespace Wanxiangshu.Next.OpenCode

open System
open System.Text.RegularExpressions
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

/// Pure classification of a fully-loaded assistant message.
/// Empty formal text or formal text that contains XML markup (including broken
/// tags) is interaction repair (continuation), never durable fallback.
module CompletedTurnClassifier =

    // Containment, not well-formedness: broken/partial tags still count.
    let private xmlMarkup =
        Regex(
            "<(?:/?\\s*(?:tool_call|use_tool|call|function_call|invoke)\\b[^>]*>?)|</(?:tool_call|use_tool|call|function_call|invoke)\\s*>",
            RegexOptions.Compiled ||| RegexOptions.IgnoreCase
        )

    let private containsXmlMarkup (text: string) =
        not (String.IsNullOrWhiteSpace text) && xmlMarkup.IsMatch text

    let private supportsInteractionRepair =
        function
        | Some AgentRole.Manager
        | Some AgentRole.Orchestrator
        | Some AgentRole.Coder
        | Some AgentRole.Reviewer
        | Some AgentRole.Inspector
        | Some AgentRole.DevOps
        | Some AgentRole.Browser
        | Some AgentRole.Meditator -> true
        | Some AgentRole.Executor
        | Some AgentRole.Blogger
        | None -> false

    let private asString (value: obj) =
        if isNull value then
            None
        else
            let text = unbox<string> value
            if String.IsNullOrWhiteSpace text then None else Some text

    /// Formal visible assistant text only (no reasoning/thinking).
    /// Used for empty / contains-XML repair classification and finish=stop emptiness.
    let partsText (parts: obj array) : string =
        Projection.formalTextFromParts parts

    /// Session A material: formal text + host-visible reasoning/thinking.
    /// Still excludes tool raw streams / tool results.
    let partsSessionA (parts: obj array) : string =
        if isNull parts then
            ""
        else
            parts
            |> Array.choose (fun part ->
                if isNull part then
                    None
                else
                    match asString part?``type`` with
                    | Some "text" -> asString part?text
                    | Some "reasoning"
                    | Some "thinking" ->
                        asString part?text
                        |> Option.orElse (asString part?reasoning)
                        |> Option.orElse (asString part?thinking)
                    | _ -> None)
            |> String.concat "\n\n"

    let hasToolCallPart (parts: obj array) : bool =
        if isNull parts then
            false
        else
            parts
            |> Array.exists (fun part ->
                if isNull part then
                    false
                else
                    match asString part?``type`` with
                    | Some kind ->
                        kind = "tool-call"
                        || kind = "tool_call"
                        || kind = "tool"
                        || kind = "patch"
                        || kind = "step-start"
                        || kind = "step-finish"
                    | None -> false)

    let isAbortErrorName (name: string option) =
        match name with
        | Some value ->
            let lower = value.ToLowerInvariant()
            lower.Contains("abort")
        | None -> false

    let classifyOutcome (finish: string option) (errorName: string option) (parts: obj array) : TurnOutcome =
        if isAbortErrorName errorName then
            TurnAborted(defaultArg errorName "aborted")
        elif
            finish
            |> Option.exists (fun value -> value.Equals("aborted", StringComparison.OrdinalIgnoreCase))
        then
            TurnAborted("finish=aborted")
        else
            match finish with
            | Some value when value.Equals("error", StringComparison.OrdinalIgnoreCase) ->
                if isAbortErrorName errorName then
                    TurnAborted(defaultArg errorName "aborted")
                else
                    TurnFailed(defaultArg errorName "assistant finish=error")
            | Some value when value.Equals("stop", StringComparison.OrdinalIgnoreCase) ->
                let text = partsText parts

                // A terminal provider step is not a final answer unless it has
                // formal natural-language text. Reasoning/tool bookkeeping may
                // be present while the model-visible answer is still empty.
                // Formal text that contains XML markup (including broken tags)
                // is also not a final report; it is interaction repair, not
                // durable provider fallback.
                if String.IsNullOrWhiteSpace text then
                    TurnNeedsContinuation "assistant stop without formal text"
                elif containsXmlMarkup text then
                    TurnNeedsContinuation "assistant stop with XML markup"
                else
                    TurnCompleted
            | Some value when value.Equals("tool-calls", StringComparison.OrdinalIgnoreCase) -> TurnInProgress
            | Some value when value.Equals("length", StringComparison.OrdinalIgnoreCase) ->
                TurnNeedsContinuation "assistant finish=length"
            | Some value -> TurnFailed(sprintf "assistant finish=%s" value)
            // No finish yet: Unknown. Never invent Completed from parts alone —
            // abort/error may still be racing the idle wake-up.
            | None -> TurnUnknown

    let needsZeroWidthContinuation (role: AgentRole option) (outcome: TurnOutcome) (_parts: obj array) =
        supportsInteractionRepair role
        && (match outcome with
            | TurnInProgress
            | TurnNeedsContinuation _ -> true
            | TurnCompleted
            | TurnAborted _
            | TurnFailed _
            | TurnUnknown -> false)

    let roleOfAgent (agent: string option) (fallback: AgentRole option) =
        match agent with
        | Some value -> HostSessionContext.roleOf value |> Option.orElse fallback
        | None -> fallback

    let buildTurn
        (sessionId: SessionId)
        (userMessageId: MessageId)
        (rootUserMessageId: MessageId)
        (assistant: SessionMessage)
        (roleFallback: AgentRole option)
        (directory: string)
        : ReconciledTurn =
        let role = roleOfAgent assistant.Agent roleFallback
        let outcome = classifyOutcome assistant.Finish assistant.ErrorName assistant.Parts

        { SessionId = sessionId
          UserMessageId = userMessageId
          RootUserMessageId = rootUserMessageId
          AssistantMessageId = assistant.Id
          AgentRole = role
          Directory = directory
          Parts = assistant.Parts
          Finish = assistant.Finish
          ErrorName = assistant.ErrorName
          Model = assistant.Model
          Outcome = outcome }
