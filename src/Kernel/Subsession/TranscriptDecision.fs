module Wanxiangshu.Kernel.Subsession.TranscriptDecision

open Wanxiangshu.Kernel.Subsession.Types

/// Decision reached by analyzing the transcript when a turn goes idle
/// without an explicit task_complete signal.
type TranscriptDecision =
    | CompleteNaturally of output: string
    | RecoverWithPrompt of prompt: string
    | ContinueNormally of prompt: string
    | IncompleteWithoutRecovery of reason: string

/// Pure function: classify CurrentTurnEvidence (turn-sliced) into a decision.
/// This is the primary path — only analyzes messages from the current turn.
let classifyTurnEvidence (evidence: CurrentTurnEvidence) : TranscriptDecision =
    match evidence.Todos with
    | TodosCompleted -> CompleteNaturally ""
    | TodosNotCompleted ->
        match evidence.Recovery with
        | RecoveryPrompt prompt -> RecoverWithPrompt prompt
        | NoRecoveryPrompt ->
            match evidence.Assistant with
            | NoAssistant ->
                IncompleteWithoutRecovery "No assistant message in current turn"
            | EmptyAssistant ->
                IncompleteWithoutRecovery "Assistant message in current turn has no content"
            | AssistantContent(text, finish) ->
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
                    IncompleteWithoutRecovery "Session idle without task completion and no recovery available"
