namespace Wanxiangshu.Kernel.Subsession.Types

open Wanxiangshu.Kernel.FallbackKernel.Types

// ── Transcript evidence ──

/// Anchor for slicing transcript to current turn only.
type TurnAnchor =
    | AnchorByUserMessageId of messageId: string
    | AnchorByHostRunId of runId: string
    | AnchorByTurnMarkerOnly

type TranscriptReadFailure = { Message: string }

/// Evidence about the current turn, accumulated from transcript slice.
/// Replaces the old boolean-based TranscriptSnapshot for turn-sliced evaluation.
type RecordedOutcome =
    | NoOutcome
    | FailureObserved of ErrorInput
    | CompletionRequested of output: string

/// Evidence about the current turn, accumulated from transcript slice.
/// Replaces the old boolean-based TranscriptSnapshot for turn-sliced evaluation.
type AssistantFinish =
    | ToolFinish
    | NormalFinish

type AssistantEvidence =
    | NoAssistant
    | EmptyAssistant
    | AssistantSnapshot of messageId: string * revision: int64 * text: string * finish: AssistantFinish option
    | AssistantDelta of messageId: string * revision: int64 * text: string * finish: AssistantFinish option

type TodoEvidence =
    | NoTodoInfo
    | TodosNotCompleted
    | TodosCompleted

type ToolEvidence =
    | NoToolResult
    | HasToolResult

type RecoveryEvidence =
    | NoRecoveryPrompt
    | RecoveryPrompt of recoveryPrompt: string
    | RawToolCallDetected of recoveryPrompt: string

type CurrentTurnEvidence =
    { Assistant: AssistantEvidence
      Todos: TodoEvidence
      Tool: ToolEvidence
      Recovery: RecoveryEvidence
      Outcome: RecordedOutcome }

module CurrentTurnEvidence =
    let empty: CurrentTurnEvidence =
        { Assistant = NoAssistant
          Todos = NoTodoInfo
          Tool = NoToolResult
          Recovery = NoRecoveryPrompt
          Outcome = NoOutcome }

    let private mergeAssistant e1 e2 =
        match e1, e2 with
        | NoAssistant, x -> x
        | x, NoAssistant -> x
        | EmptyAssistant, x -> x
        | x, EmptyAssistant -> x
        | AssistantSnapshot(id1, rev1, t1, f1), AssistantSnapshot(id2, rev2, t2, f2) ->
            if id1 <> "" && id2 <> "" && id1 = id2 then
                if rev2 >= rev1 then
                    AssistantSnapshot(id2, rev2, t2, f2)
                else
                    AssistantSnapshot(id1, rev1, t1, f1)
            elif id1 = "" && id2 = "" then
                if t2 <> "" then
                    AssistantSnapshot("", 0L, t2, f2)
                else
                    AssistantSnapshot("", 0L, t1, f1)
            elif rev2 > rev1 then
                AssistantSnapshot(id2, rev2, t2, f2)
            else
                AssistantSnapshot(id1, rev1, t1, f1)
        | AssistantDelta(id1, rev1, t1, f1), AssistantDelta(id2, rev2, t2, f2) ->
            if id1 <> "" && id2 <> "" && id1 = id2 then
                AssistantDelta(id1, max rev1 rev2, t1 + t2, (if rev2 > rev1 then f2 else f1))
            elif id1 = "" && id2 = "" then
                AssistantDelta("", max rev1 rev2, t1 + t2, (if rev2 >= rev1 then f2 else f1))
            elif rev2 > rev1 then
                AssistantDelta(id2, rev2, t2, f2)
            else
                AssistantDelta(id1, rev1, t1, f1)
        | AssistantSnapshot(id1, rev1, t1, f1), AssistantDelta(id2, rev2, t2, f2)
        | AssistantDelta(id2, rev2, t2, f2), AssistantSnapshot(id1, rev1, t1, f1) ->
            if id1 <> "" && id2 <> "" && id1 = id2 then
                if rev2 > rev1 then
                    AssistantSnapshot(id1, rev2, t1 + t2, f2)
                else
                    AssistantSnapshot(id1, rev1, t1, f1)
            elif id1 = "" && id2 = "" then
                AssistantSnapshot("", 0L, t2, f2)
            elif rev2 > rev1 then
                AssistantSnapshot(id2, rev2, t2, f2)
            else
                AssistantSnapshot(id1, rev1, t1, f1)

    let private mergeTodos e1 e2 =
        match e2 with
        | NoTodoInfo -> e1
        | _ -> e2

    let private mergeTool e1 e2 =
        match e1, e2 with
        | HasToolResult, _ -> HasToolResult
        | _, HasToolResult -> HasToolResult
        | NoToolResult, NoToolResult -> NoToolResult

    let private mergeRecovery e1 e2 =
        match e1, e2 with
        | RawToolCallDetected r1, RawToolCallDetected r2 ->
            if r1 = r2 then RawToolCallDetected r1
            elif r1 = "" then RawToolCallDetected r2
            elif r2 = "" then RawToolCallDetected r1
            else RawToolCallDetected(r1 + "\n" + r2)
        | RawToolCallDetected r, _ -> RawToolCallDetected r
        | _, RawToolCallDetected r -> RawToolCallDetected r
        | RecoveryPrompt r1, RecoveryPrompt r2 ->
            if r1 = r2 then RecoveryPrompt r1
            elif r1 = "" then RecoveryPrompt r2
            elif r2 = "" then RecoveryPrompt r1
            else RecoveryPrompt(r1 + "\n" + r2)
        | RecoveryPrompt r, NoRecoveryPrompt -> RecoveryPrompt r
        | NoRecoveryPrompt, RecoveryPrompt r -> RecoveryPrompt r
        | NoRecoveryPrompt, NoRecoveryPrompt -> NoRecoveryPrompt

    let private mergeOutcome e1 e2 =
        match e1, e2 with
        | CompletionRequested _, _ -> e1
        | _, CompletionRequested _ -> e2
        | FailureObserved _, _ -> e1
        | _, FailureObserved _ -> e2
        | NoOutcome, NoOutcome -> NoOutcome

    let merge (e1: CurrentTurnEvidence) (e2: CurrentTurnEvidence) : CurrentTurnEvidence =
        { Assistant = mergeAssistant e1.Assistant e2.Assistant
          Todos = mergeTodos e1.Todos e2.Todos
          Tool = mergeTool e1.Tool e2.Tool
          Recovery = mergeRecovery e1.Recovery e2.Recovery
          Outcome = mergeOutcome e1.Outcome e2.Outcome }

module AssistantEvidence =
    let content text finish = AssistantSnapshot("", 0L, text, finish)

    let snapshot messageId revision text finish =
        AssistantSnapshot(messageId, revision, text, finish)

    let delta messageId revision text finish =
        AssistantDelta(messageId, revision, text, finish)

    let isSnapshot =
        function
        | AssistantSnapshot _ -> true
        | _ -> false

    let isDelta =
        function
        | AssistantDelta _ -> true
        | _ -> false

    let isContent =
        function
        | AssistantSnapshot _
        | AssistantDelta _ -> true
        | _ -> false
