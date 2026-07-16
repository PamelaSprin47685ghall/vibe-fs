module Wanxiangshu.Kernel.Subsession.TranscriptDecision

open Wanxiangshu.Kernel.Subsession.Types

/// Decision reached by analyzing the transcript when a turn goes idle
/// without an explicit task_complete signal.
type TranscriptDecision =
    | CompleteNaturally of output: string
    | RecoverWithPrompt of prompt: string
    | ContinueNormally of prompt: string
    | IncompleteWithoutRecovery of reason: string

/// Zero-width prompt injected when the assistant produced no usable output,
/// mirroring the main-session `isIdleNoContentAndNoTools → SendContinue` path.
let private emptyTurnPrompt = "\u200B"

/// Pure function: classify CurrentTurnEvidence (turn-sliced) into a decision.
/// This is the primary path — only analyzes messages from the current turn.
let classifyTurnEvidence (evidence: CurrentTurnEvidence) : TranscriptDecision =
    match evidence.Outcome with
    | CompletionRequested output -> CompleteNaturally output
    | FailureObserved err -> IncompleteWithoutRecovery err.Message
    | NoOutcome ->
        match evidence.Todos with
        | TodosCompleted ->
            match evidence.Assistant with
            | AssistantSnapshot(_, _, text, _)
            | AssistantDelta(_, _, text, _) when not (System.String.IsNullOrWhiteSpace text) -> CompleteNaturally text
            | _ -> CompleteNaturally ""
        | TodosNotCompleted
        | NoTodoInfo ->
            match evidence.Recovery with
            | RawToolCallDetected prompt
            | RecoveryPrompt prompt -> RecoverWithPrompt prompt
            | NoRecoveryPrompt ->
                match evidence.Assistant with
                | NoAssistant -> ContinueNormally emptyTurnPrompt
                | EmptyAssistant -> ContinueNormally emptyTurnPrompt
                | AssistantSnapshot(_, _, text, finish)
                | AssistantDelta(_, _, text, finish) ->
                    if System.String.IsNullOrWhiteSpace text then
                        ContinueNormally emptyTurnPrompt
                    else
                        let toolFinish =
                            match finish with
                            | Some ToolFinish -> true
                            | _ -> false

                        let hasToolResult =
                            match evidence.Tool with
                            | HasToolResult -> true
                            | NoToolResult -> false

                        let taskComplete = (not toolFinish) || hasToolResult

                        if taskComplete then
                            CompleteNaturally text
                        else
                            ContinueNormally emptyTurnPrompt
