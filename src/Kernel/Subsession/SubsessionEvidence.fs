namespace Wanxiangshu.Kernel.Subsession.Types

open Wanxiangshu.Kernel.FallbackKernel.Types

// ── Transcript evidence ──

type TranscriptReadFailure = { Message: string }

/// Evidence about the current turn, accumulated from transcript slice.
/// Replaces the old boolean-based TranscriptSnapshot for turn-sliced evaluation.
type RecordedOutcome =
    | NoOutcome
    | FailureObserved of ErrorInput
    | CompletionRequested of output: string

type AssistantEvidence =
    | NoAssistant
    | EmptyAssistant
    | AssistantSnapshot of messageId: string * revision: int64 * text: string
    | AssistantDelta of messageId: string * revision: int64 * text: string

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
      Outcome: RecordedOutcome
      IdleObserved: bool }

module CurrentTurnEvidence =
    let empty: CurrentTurnEvidence =
        { Assistant = NoAssistant
          Todos = NoTodoInfo
          Tool = NoToolResult
          Recovery = NoRecoveryPrompt
          Outcome = NoOutcome
          IdleObserved = false }

    let private mergeAssistant e1 e2 =
        match e1, e2 with
        | NoAssistant, x -> x
        | x, NoAssistant -> x
        | EmptyAssistant, x -> x
        | x, EmptyAssistant -> x
        | AssistantSnapshot(id1, rev1, t1), AssistantSnapshot(id2, rev2, t2) ->
            if id1 <> "" && id2 <> "" && id1 = id2 then
                if rev2 >= rev1 then
                    AssistantSnapshot(id2, rev2, t2)
                else
                    AssistantSnapshot(id1, rev1, t1)
            elif id1 = "" && id2 = "" then
                if t2 <> "" then
                    AssistantSnapshot("", 0L, t2)
                else
                    AssistantSnapshot("", 0L, t1)
            elif rev2 > rev1 then
                AssistantSnapshot(id2, rev2, t2)
            else
                AssistantSnapshot(id1, rev1, t1)
        | AssistantDelta(id1, rev1, t1), AssistantDelta(id2, rev2, t2) ->
            if id1 <> "" && id2 <> "" && id1 = id2 then
                AssistantDelta(id1, max rev1 rev2, t1 + t2)
            elif id1 = "" && id2 = "" then
                AssistantDelta("", max rev1 rev2, t1 + t2)
            elif rev2 > rev1 then
                AssistantDelta(id2, rev2, t2)
            else
                AssistantDelta(id1, rev1, t1)
        | AssistantSnapshot(id1, rev1, t1), AssistantDelta(id2, rev2, t2)
        | AssistantDelta(id2, rev2, t2), AssistantSnapshot(id1, rev1, t1) ->
            if id1 <> "" && id2 <> "" && id1 = id2 then
                if rev2 > rev1 then
                    AssistantSnapshot(id1, rev2, t1 + t2)
                else
                    AssistantSnapshot(id1, rev1, t1)
            elif id1 = "" && id2 = "" then
                AssistantSnapshot("", 0L, t2)
            elif rev2 > rev1 then
                AssistantSnapshot(id2, rev2, t2)
            else
                AssistantSnapshot(id1, rev1, t1)

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
          Outcome = mergeOutcome e1.Outcome e2.Outcome
          IdleObserved = e1.IdleObserved || e2.IdleObserved }

module AssistantEvidence =
    let content text = AssistantSnapshot("", 0L, text)

    let snapshot messageId revision text =
        AssistantSnapshot(messageId, revision, text)

    let delta messageId revision text =
        AssistantDelta(messageId, revision, text)

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
