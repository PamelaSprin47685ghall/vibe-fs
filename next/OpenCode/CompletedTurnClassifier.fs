namespace Wanxiangshu.Next.OpenCode

open System
open System.Text.RegularExpressions
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

/// Pure classification of a fully-loaded assistant message.
/// Empty/XML-only is interaction repair (continuation), never durable fallback.
module CompletedTurnClassifier =

    let private xmlTag =
        Regex("<(?:tool_call|use_tool|call|function_call|invoke)[^>]*>", RegexOptions.Compiled)

    let private asString (value: obj) =
        if isNull value then
            None
        else
            let text = unbox<string> value
            if String.IsNullOrWhiteSpace text then None else Some text

    let partsText (parts: obj array) : string =
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
                    | _ -> None)
            |> String.concat ""

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

                if String.IsNullOrWhiteSpace text && not (hasToolCallPart parts) then
                    TurnNeedsContinuation "assistant stop with empty text"
                else
                    TurnCompleted
            | Some value when value.Equals("tool-calls", StringComparison.OrdinalIgnoreCase) -> TurnInProgress
            | Some value when value.Equals("length", StringComparison.OrdinalIgnoreCase) ->
                TurnNeedsContinuation "assistant finish=length"
            | Some value -> TurnFailed(sprintf "assistant finish=%s" value)
            // No finish yet: Unknown. Never invent Completed from parts alone —
            // abort/error may still be racing the idle wake-up.
            | None -> TurnUnknown

    let needsZeroWidthContinuation (role: AgentRole option) (outcome: TurnOutcome) (parts: obj array) =
        match outcome, role with
        | TurnInProgress, _ -> true
        | TurnNeedsContinuation _, Some AgentRole.Manager
        | TurnNeedsContinuation _, Some AgentRole.Orchestrator
        | TurnNeedsContinuation _, Some AgentRole.Coder
        | TurnNeedsContinuation _, Some AgentRole.Reviewer
        | TurnNeedsContinuation _, Some AgentRole.Inspector
        | TurnNeedsContinuation _, Some AgentRole.Browser
        | TurnNeedsContinuation _, Some AgentRole.Meditator -> true
        | TurnCompleted, Some AgentRole.Manager
        | TurnCompleted, Some AgentRole.Orchestrator
        | TurnCompleted, Some AgentRole.Coder
        | TurnCompleted, Some AgentRole.Reviewer
        | TurnCompleted, Some AgentRole.Inspector
        | TurnCompleted, Some AgentRole.Browser
        | TurnCompleted, Some AgentRole.Meditator ->
            let text = partsText parts
            let hasTool = hasToolCallPart parts

            (String.IsNullOrWhiteSpace text && not hasTool)
            || (xmlTag.IsMatch text && not hasTool)
        | _ -> false

    let roleOfAgent (agent: string option) (fallback: AgentRole option) =
        match agent with
        | Some value -> HostSessionContext.roleOf value |> Option.orElse fallback
        | None -> fallback

    let buildTurn
        (sessionId: SessionId)
        (userMessageId: MessageId)
        (assistant: SessionMessage)
        (roleFallback: AgentRole option)
        (directory: string)
        : ReconciledTurn =
        let role = roleOfAgent assistant.Agent roleFallback
        let outcome = classifyOutcome assistant.Finish assistant.ErrorName assistant.Parts

        { SessionId = sessionId
          UserMessageId = userMessageId
          AssistantMessageId = assistant.Id
          AgentRole = role
          Directory = directory
          Parts = assistant.Parts
          Finish = assistant.Finish
          ErrorName = assistant.ErrorName
          Model = assistant.Model
          Outcome = outcome }
