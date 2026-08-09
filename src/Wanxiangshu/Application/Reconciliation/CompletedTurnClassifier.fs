namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Pure classification of a fully-loaded assistant message.
/// Empty formal text or formal text that contains XML markup (including broken
/// tags) is interaction repair (continuation), never durable fallback.
module CompletedTurnClassifier =

    let private supportsInteractionRepair =
        function
        | Some Role.Manager
        | Some Role.Orchestrator
        | Some Role.Coder
        | Some Role.Reviewer
        | Some Role.Inspector
        | Some Role.DevOps
        | Some Role.Browser
        | Some Role.Meditator
        | Some Role.Student
        | Some Role.Teacher -> true
        | Some Role.Executor
        | Some Role.Blogger
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

    /// Session terminal material: formal text + host-visible reasoning/thinking.
    /// COMPANION-003: TerminalOutputRaw 与 LWR 禁止 raw tool call/result——
    /// 末回合工具可能极大且不经 transform。This is the XTrace terminal segment's
    /// text, not a parallel A channel.
    let partsSessionText (parts: MessagePart array) : string =
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

    /// Returns either a publishable `TurnOutcome` or a private `SnapshotObservation`.
    /// Heterogeneous `obj` so finish=None stays instanceof SnapshotObservation in JS
    /// (HOST-004 Clean Break — must not mint TurnOutcome.TurnUnknown).
    let classifyOutcome
        (completed: bool)
        (finish: string option)
        (errorName: string option)
        (parts: MessagePart array)
        : obj =
        if isAbortErrorName errorName then
            box (ReconcileProgram.TurnAborted(defaultArg errorName "aborted"))
        elif completed && Option.isSome errorName then
            box (ReconcileProgram.TurnFailed(defaultArg errorName "assistant completed with error"))
        elif
            finish
            |> Option.exists (fun value -> value.Equals("aborted", StringComparison.OrdinalIgnoreCase))
        then
            box (ReconcileProgram.TurnAborted("finish=aborted"))
        else
            match finish with
            | Some value when value.Equals("error", StringComparison.OrdinalIgnoreCase) ->
                if isAbortErrorName errorName then
                    box (ReconcileProgram.TurnAborted(defaultArg errorName "aborted"))
                else
                    box (ReconcileProgram.TurnFailed(defaultArg errorName "assistant finish=error"))
            | Some value when value.Equals("stop", StringComparison.OrdinalIgnoreCase) ->
                // CTX-004: a terminal provider step is not a final answer unless
                // it carries usable formal text. `TerminalValidity` is the single
                // owner of that predicate — reasoning and tool bookkeeping may be
                // present while the model-visible answer is still empty or is
                // tool-call markup, and both cases earn interaction repair rather
                // than durable provider fallback.
                match TerminalValidity.check (partsText parts) with
                | Ok() -> box ReconcileProgram.TurnCompleted
                | Error rejection ->
                    box (
                        ReconcileProgram.TurnNeedsContinuation(
                            sprintf "assistant stop with %s" (TerminalValidity.describe rejection)
                        )
                    )
            | Some value when value.Equals("tool-calls", StringComparison.OrdinalIgnoreCase) ->
                box ReconcileProgram.TurnInProgress
            | Some value when value.Equals("length", StringComparison.OrdinalIgnoreCase) ->
                box (ReconcileProgram.TurnNeedsContinuation "assistant finish=length")
            | Some value -> box (ReconcileProgram.TurnFailed(sprintf "assistant finish=%s" value))
            // No finish yet: private SnapshotObservation. Never invent Completed
            // from parts alone — abort/error may still be racing the idle wake-up.
            | None -> box ReconcileProgram.TurnUnknown

    /// ARCH-011: named for the typed occasion (unfinished interaction), not for any
    /// character feature of the repair payload. `_parts` is deliberately ignored: a
    /// `TurnInProgress`/`TurnNeedsContinuation` outcome already carries the decision.
    /// Accepts `obj` because classifyOutcome may return SnapshotObservation.
    let needsInteractionRepair (role: Role option) (classified: obj) (_parts: MessagePart array) =
        supportsInteractionRepair role
        && (match classified with
            | :? ReconcileProgram.TurnOutcome as outcome ->
                match outcome with
                | ReconcileProgram.TurnInProgress
                | ReconcileProgram.TurnNeedsContinuation _ -> true
                | ReconcileProgram.TurnCompleted
                | ReconcileProgram.TurnAborted _
                | ReconcileProgram.TurnFailed _ -> false
            | _ -> false)

    let roleOfAgent (agent: string option) (fallback: Role option) =
        match agent with
        | Some value -> HostSessionContext.roleOf value |> Option.orElse fallback
        | None -> fallback

    let buildTurn
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (authorityRoot: AuthorityRootUserMessageId)
        (assistant: SessionMessage)
        (roleFallback: Role option)
        (directory: string option)
        : ReconciledTurn =
        let role = roleOfAgent assistant.Agent roleFallback

        let classified =
            classifyOutcome assistant.Completed assistant.Finish assistant.ErrorName assistant.Parts

        // When Observation is Some, Outcome is an unreachable placeholder for
        // callers that only match publishable TurnOutcome cases (evidence and
        // missing-final-report consult Observation first).
        let outcome, observation =
            match classified with
            | :? ReconcileProgram.SnapshotObservation as obs ->
                ReconcileProgram.TurnNeedsContinuation "private-snapshot-observation", Some obs
            | :? ReconcileProgram.TurnOutcome as o -> o, None
            | _ -> failwith "classifyOutcome returned neither TurnOutcome nor SnapshotObservation"

        { SessionId = sessionId
          PhysicalUserMessageId = physicalUserMessageId
          AuthorityRootUserMessageId = authorityRoot
          // HOST-010: the assistant message IS the provider run.
          ProviderRun = ProviderRunIdentity.create assistant.Id
          Role = role
          Directory = directory
          Parts = assistant.Parts
          Finish = assistant.Finish
          ErrorName = assistant.ErrorName
          Model = assistant.Model
          Outcome = outcome
          Observation = observation }
