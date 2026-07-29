namespace Wanxiangshu.Next.OpenCode

open System
open System.Text.RegularExpressions
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

    /// Formal visible assistant text only (no reasoning/thinking).
    /// Used for empty/XML-only repair classification and finish=stop emptiness.
    let partsText (parts: MessagePart array) : string =
        if isNull parts then
            ""
        else
            parts
            |> Array.choose (function
                | MessagePart.Text text -> Some text
                | _ -> None)
            |> String.concat ""

    /// Session A material: formal text + host-visible reasoning/thinking.
    /// Still excludes tool raw streams / tool results.
    let partsSessionA (parts: MessagePart array) : string =
        if isNull parts then
            ""
        else
            parts
            |> Array.choose (function
                | MessagePart.Text text
                | MessagePart.Reasoning text -> Some text
                | _ -> None)
            |> String.concat "\n\n"

    let hasToolCallPart (parts: MessagePart array) : bool =
        if isNull parts then
            false
        else
            parts
            |> Array.exists (function
                | MessagePart.ToolCall _ -> true
                | MessagePart.Activity kind -> kind = "patch" || kind = "step-start" || kind = "step-finish"
                | _ -> false)

    let isAbortErrorName (name: string option) =
        match name with
        | Some value ->
            let lower = value.ToLowerInvariant()
            lower.Contains("abort")
        | None -> false

    let classifyOutcome (finish: string option) (errorName: string option) (parts: MessagePart array) : TurnOutcome =
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

    let needsZeroWidthContinuation (role: AgentRole option) (outcome: TurnOutcome) (_parts: MessagePart array) =
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
        (physicalUserMessageId: PhysicalUserMessageId)
        (authorityRoot: AuthorityRootUserMessageId)
        (assistant: SessionMessage)
        (roleFallback: AgentRole option)
        (directory: string option)
        : ReconciledTurn =
        let role = roleOfAgent assistant.Agent roleFallback
        let outcome = classifyOutcome assistant.Finish assistant.ErrorName assistant.Parts

        { SessionId = sessionId
          PhysicalUserMessageId = physicalUserMessageId
          AuthorityRootUserMessageId = authorityRoot
          // HOST-010: the assistant message IS the provider run.
          ProviderRun = ProviderRunIdentity.create assistant.Id
          AgentRole = role
          Directory = directory
          Parts = assistant.Parts
          Finish = assistant.Finish
          ErrorName = assistant.ErrorName
          Model = assistant.Model
          Outcome = outcome }
