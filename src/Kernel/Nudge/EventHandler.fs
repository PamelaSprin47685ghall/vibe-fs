module Wanxiangshu.Kernel.Nudge.EventHandler

open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Nudge.Transitions

let handleSessionNextStepEnded state finish sessionID =
    let state = deleteRetryPendingSession state sessionID
    if finish <> "" && isTerminalAssistantFinish finish then
        tryClaimNudge state sessionID
    else
        state, false

let handleEvent (state: NudgeShellState) (sessionID: string) (event: NudgeHostEvent) : NudgeShellState * bool =
    match event with
    | StreamAbort -> clearSession state sessionID, false
    | SessionDeleted -> clearSession state sessionID, false
    | SessionNextPrompted promptText ->
        (if isNudgePrompt promptText then state else resumeSession state sessionID), false
    | SessionNextRetried -> addRetryPendingSession state sessionID, false
    | MessageUpdated UpdateAborted -> stopSession state sessionID, false
    | MessageUpdated UpdateCompletedAssistant -> tryClaimNudge state sessionID
    | MessageUpdated UpdateNoChange -> state, false
    | MessagePartUpdated PartRetry -> addRetryPendingSession state sessionID, false
    | MessagePartUpdated PartAborted -> stopSession state sessionID, false
    | MessagePartUpdated PartRetryProgress -> deleteRetryPendingSession state sessionID, false
    | MessagePartUpdated PartOther -> state, false
    | SessionNextStepFailed StepFailAbort -> stopSession state sessionID, false
    | SessionNextStepFailed StepFailOther -> state, false
    | SessionNextToolFailed ToolFailAbort -> stopSession state sessionID, false
    | SessionNextToolFailed ToolFailOther -> deleteRetryPendingSession state sessionID, false
    | SessionNextStepEnded finish -> handleSessionNextStepEnded state finish sessionID
    | SessionIdle -> tryClaimNudge state sessionID
    | SessionError SessionErrorAbort -> stopSession state sessionID, false
    | SessionError SessionErrorOther -> addRetryPendingSession state sessionID, false
    | SessionStatusIdle -> tryClaimNudge state sessionID
    | SessionStatusBusy -> handleSessionBusy state sessionID, false
    | SessionStatusRetry -> addRetryPendingSession state sessionID, false
    | RetryProgress -> deleteRetryPendingSession state sessionID, false
    | Other -> state, false
