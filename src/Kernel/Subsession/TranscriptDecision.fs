module Wanxiangshu.Kernel.Subsession.TranscriptDecision

open Wanxiangshu.Kernel.Subsession.Types

/// Decision reached by analyzing the transcript when a turn goes idle
/// without an explicit task_complete signal.
type TranscriptDecision =
    | CompleteNaturally of output: string
    | RecoverWithPrompt of prompt: string
    | ContinueNormally of prompt: string
    | IncompleteWithoutRecovery of reason: string

/// Pure function: classify a transcript snapshot into a decision.
///
/// Mirrors the existing logic from FallbackMessageCodec:
///   1. allTodosCompleted → CompleteNaturally
///   2. scanToolCallAsText → RecoverWithPrompt
///   3. (not isToolFinish) || hasToolResult → CompleteNaturally
///   4. otherwise → IncompleteWithoutRecovery
let classifyTranscript (snap: TranscriptSnapshot) : TranscriptDecision =
    if snap.AllTodosCompleted then
        CompleteNaturally snap.LastAssistantText
    else
        match snap.ToolCallAsTextRecoveryPrompt with
        | Some prompt -> RecoverWithPrompt prompt
        | None ->
            let taskComplete =
                (not snap.LastAssistantToolFinish) || snap.HasToolResultAfterLastAssistant

            if taskComplete then
                CompleteNaturally snap.LastAssistantText
            else
                IncompleteWithoutRecovery "Session idle without task completion and no recovery available"
