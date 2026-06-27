module Wanxiangshu.Tests.NudgeEventHandlerTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Nudge.EventHandler

let private sv: NudgeShellState = emptyState
let private sid = "session1"

let emptyStateHasEmptySets () =
    equal "nudgedSessions" Set.empty sv.nudgedSessions
    equal "stoppedSessions" Set.empty sv.stoppedSessions
    equal "retryPending" Set.empty sv.retryPendingSessions
    equal "lastNudgedSession" None sv.lastNudgedSession

let handleStreamAbortClears () =
    let st = { sv with stoppedSessions = Set.ofList [ sid ] }
    let st', claimed = handleEvent st sid StreamAbort
    equal "not claimed" false claimed
    check "stopped removed" (not (Set.contains sid st'.stoppedSessions))

let handleSessionDeletedClears () =
    let st = { sv with stoppedSessions = Set.ofList [ sid ] }
    let st', _ = handleEvent st sid SessionDeleted
    check "stopped removed" (not (Set.contains sid st'.stoppedSessions))

let handleSessionNextPromptedNudgePrompt () =
    let st = { sv with stoppedSessions = Set.ofList [ sid ] }
    // Use actual nudge prompt constant to match exact string check in isNudgePrompt
    let st', _ = handleEvent st sid (SessionNextPrompted "There are still incomplete todos. Continue working through the remaining items. If they are irrelevant, remove them. If you want to skip this check, respond with <skip-todo-check />")
    check "nudge prompt: stopped remains" (Set.contains sid st'.stoppedSessions)

let handleSessionNextPromptedNormalPrompt () =
    let st = { sv with stoppedSessions = Set.ofList [ sid ] }
    let st', _ = handleEvent st sid (SessionNextPrompted "hello")
    check "normal prompt: stopped cleared" (not (Set.contains sid st'.stoppedSessions))

let handleSessionNextRetried () =
    let st = { sv with retryPendingSessions = Set.empty }
    let st', _ = handleEvent st sid SessionNextRetried
    check "retry added" (Set.contains sid st'.retryPendingSessions)

let handleMessageUpdatedAborted () =
    let st = { sv with stoppedSessions = Set.empty }
    let st', _ = handleEvent st sid (MessageUpdated UpdateAborted)
    check "stopped added" (Set.contains sid st'.stoppedSessions)

let handleMessageUpdatedCompletedAssistant () =
    let st = { sv with nudgedSessions = Set.empty }
    let st', claimed = handleEvent st sid (MessageUpdated UpdateCompletedAssistant)
    check "claimed true" claimed
    check "nudged" (Set.contains sid st'.nudgedSessions)

let handleMessageUpdatedNoChange () =
    let st = { sv with nudgedSessions = Set.empty }
    let st', claimed = handleEvent st sid (MessageUpdated UpdateNoChange)
    equal "not claimed" false claimed

let handlePartRetry () =
    let st', _ = handleEvent sv sid (MessagePartUpdated PartRetry)
    check "retry pending" (Set.contains sid st'.retryPendingSessions)

let handlePartAborted () =
    let st', _ = handleEvent sv sid (MessagePartUpdated PartAborted)
    check "stopped" (Set.contains sid st'.stoppedSessions)

let handlePartRetryProgress () =
    let st = { sv with retryPendingSessions = Set.ofList [ sid ] }
    let st', _ = handleEvent st sid (MessagePartUpdated PartRetryProgress)
    check "retry removed" (not (Set.contains sid st'.retryPendingSessions))

let handlePartOther () =
    let st', claimed = handleEvent sv sid (MessagePartUpdated PartOther)
    equal "not claimed" false claimed

let handleStepFailAbort () =
    let st', _ = handleEvent sv sid (SessionNextStepFailed StepFailAbort)
    check "stopped" (Set.contains sid st'.stoppedSessions)

let handleStepFailOther () =
    let st', claimed = handleEvent sv sid (SessionNextStepFailed StepFailOther)
    equal "not claimed" false claimed

let handleToolFailAbort () =
    let st', _ = handleEvent sv sid (SessionNextToolFailed ToolFailAbort)
    check "stopped" (Set.contains sid st'.stoppedSessions)

let handleToolFailOther () =
    let st = { sv with retryPendingSessions = Set.ofList [ sid ] }
    let st', _ = handleEvent st sid (SessionNextToolFailed ToolFailOther)
    check "retry removed" (not (Set.contains sid st'.retryPendingSessions))

let handleSessionIdle () =
    let st', claimed = handleEvent sv sid SessionIdle
    check "claimed true" claimed

let handleSessionErrorAbort () =
    let st', _ = handleEvent sv sid (SessionError SessionErrorAbort)
    check "stopped" (Set.contains sid st'.stoppedSessions)

let handleSessionErrorOther () =
    let st', _ = handleEvent sv sid (SessionError SessionErrorOther)
    check "retry added" (Set.contains sid st'.retryPendingSessions)

let handleSessionStatusIdle () =
    let st', claimed = handleEvent sv sid SessionStatusIdle
    check "claimed" claimed

let handleSessionStatusBusy () =
    let st', claimed = handleEvent sv sid SessionStatusBusy
    equal "not claimed" false claimed

let handleSessionStatusRetry () =
    let st', _ = handleEvent sv sid SessionStatusRetry
    check "retry added" (Set.contains sid st'.retryPendingSessions)

let handleRetryProgress () =
    let st = { sv with retryPendingSessions = Set.ofList [ sid ] }
    let st', _ = handleEvent st sid RetryProgress
    check "retry removed" (not (Set.contains sid st'.retryPendingSessions))

let handleOther () =
    let st', claimed = handleEvent sv sid Other
    equal "not claimed" false claimed

let handleSessionNextStepEndedEmptyFinish () =
    let st = { sv with retryPendingSessions = Set.ofList [ sid ] }
    let st', claimed = handleEvent st sid (SessionNextStepEnded "")
    equal "retry cleared" (not (Set.contains sid st'.retryPendingSessions)) true
    equal "not claimed" false claimed

let handleSessionNextStepEndedTerminal () =
    let st', claimed = handleEvent sv sid (SessionNextStepEnded "finished")
    check "claimed" claimed

let run () =
    emptyStateHasEmptySets ()
    handleStreamAbortClears ()
    handleSessionDeletedClears ()
    handleSessionNextPromptedNudgePrompt ()
    handleSessionNextPromptedNormalPrompt ()
    handleSessionNextRetried ()
    handleMessageUpdatedAborted ()
    handleMessageUpdatedCompletedAssistant ()
    handleMessageUpdatedNoChange ()
    handlePartRetry ()
    handlePartAborted ()
    handlePartRetryProgress ()
    handlePartOther ()
    handleStepFailAbort ()
    handleStepFailOther ()
    handleToolFailAbort ()
    handleToolFailOther ()
    handleSessionIdle ()
    handleSessionErrorAbort ()
    handleSessionErrorOther ()
    handleSessionStatusIdle ()
    handleSessionStatusBusy ()
    handleSessionStatusRetry ()
    handleRetryProgress ()
    handleOther ()
    handleSessionNextStepEndedEmptyFinish ()
    handleSessionNextStepEndedTerminal ()
