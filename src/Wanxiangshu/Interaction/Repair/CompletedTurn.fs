namespace Wanxiangshu.Interaction.Repair

open System
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Composition.Turn
open Wanxiangshu.OpenCode

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
        | Some Role.Inquiry -> true
        | Some Role.Distiller
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

    /// CTX-004: stop is completed only when formal text passes the shared
    /// terminal validity gate; otherwise the turn earns interaction repair.
    let private classifyStopped (parts: MessagePart array) : obj =
        match TerminalValidity.check (partsText parts) with
        | Ok() -> box ReconcileProgram.TurnCompleted
        | Error rejection ->
            box (
                ReconcileProgram.TurnNeedsContinuation(
                    sprintf "assistant stop with %s" (TerminalValidity.describe rejection)
                )
            )

    /// Returns either a publishable `TurnOutcome` or a private `SnapshotObservation`.
    /// Heterogeneous `obj` so finish=None stays instanceof SnapshotObservation in JS
    /// (HOST-004 Clean Break — must not mint TurnOutcome.TurnUnknown).
    let classifyOutcome
        (completed: bool)
        (finish: string option)
        (errorName: string option)
        (parts: MessagePart array)
        : obj =
        match isAbortErrorName errorName, completed && Option.isSome errorName, finish with
        | true, _, _ -> box (ReconcileProgram.TurnAborted(defaultArg errorName "aborted"))
        | false, true, _ -> box (ReconcileProgram.TurnFailed(defaultArg errorName "assistant completed with error"))
        | false, false, Some value when value.Equals("aborted", StringComparison.OrdinalIgnoreCase) ->
            box (ReconcileProgram.TurnAborted("finish=aborted"))
        | false, false, Some value when value.Equals("error", StringComparison.OrdinalIgnoreCase) ->
            box (ReconcileProgram.TurnFailed(defaultArg errorName "assistant finish=error"))
        | false, false, Some value when value.Equals("stop", StringComparison.OrdinalIgnoreCase) ->
            classifyStopped parts
        | false, false, Some value when value.Equals("tool-calls", StringComparison.OrdinalIgnoreCase) ->
            box ReconcileProgram.TurnInProgress
        | false, false, Some value when value.Equals("length", StringComparison.OrdinalIgnoreCase) ->
            box (ReconcileProgram.TurnNeedsContinuation "assistant finish=length")
        | false, false, Some value -> box (ReconcileProgram.TurnFailed(sprintf "assistant finish=%s" value))
        // No finish yet: private SnapshotObservation. Never invent Completed
        // from parts alone — abort/error may still be racing the idle wake-up.
        | false, false, None -> box ReconcileProgram.TurnUnknown

    /// ARCH-011: named for the typed occasion (unfinished interaction), not for any
    /// character feature of the repair payload. A normal `finish=tool-calls` turn is
    /// still owned by the Host provider/tool loop; its concrete tool/activity part is
    /// proof that continuation is already in flight and must never be pre-empted by a
    /// synthetic InteractionRepair. Only an in-progress turn with no such Host work,
    /// or an explicit NeedsContinuation, earns repair.
    /// Accepts `obj` because classifyOutcome may return SnapshotObservation.
    let needsInteractionRepair (role: Role option) (classified: obj) (parts: MessagePart array) =
        supportsInteractionRepair role
        && (match classified with
            | :? ReconcileProgram.TurnOutcome as outcome ->
                match outcome with
                | ReconcileProgram.TurnInProgress -> not (hasToolCallPart parts)
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
