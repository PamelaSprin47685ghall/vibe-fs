module Wanxiangshu.Tests.IntegrationEventTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.IntegrationEventTestsMux
open Wanxiangshu.Tests.IntegrationEventTestsMuxReview
open Wanxiangshu.Tests.IntegrationEventTestsMuxForceStop
open Wanxiangshu.Tests.IntegrationEventTestsMuxWrappers
open Wanxiangshu.Tests.IntegrationEventTestsOpencode
open Wanxiangshu.Tests.IntegrationEventTestsOpencodeSessionStatus
open Wanxiangshu.Tests.IntegrationEventTestsOpencodeSessionStatusForceStop
open Wanxiangshu.Tests.IntegrationEventTestsOpencodeSessionStatusRepeated
open Wanxiangshu.Tests.IntegrationEventTestsOpencodeLoop
open Wanxiangshu.Tests.IntegrationEventTestsOpencodeFallback
open Wanxiangshu.Tests.IntegrationEventTestsOpencodeFallbackInterrupted
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Mux.Plugin
open Wanxiangshu.Opencode.Plugin
open Wanxiangshu.Shell.Dyn

let eventHookSpec (reg: obj) =
    promise {
        let hook = get reg "eventHook"
        check "eventHook.length === 2" (unbox<int> (hook?length) = 2)

        let ehResult =
            hook
            $ (createObj [ "type", box "stream-abort"; "workspaceId", box "test-ws" ], null)

        check "eventHook returns Promise" (not (isNullish (get ehResult "then")))
        do! unbox<JS.Promise<unit>> ehResult
    }

let run () : JS.Promise<unit> =
    promise {
        let reg = createRegistration (createObj [])
        do! eventHookSpec reg
        do! repeatedTodoNudgeSpec ()
        do! reviewerReviseRenudgesLoopSpec ()
        do! muxSubmitReviewWipDoesNotSuppressLoopNudgeSpec ()
        do! syntaxWrapperSpec reg
        do! todoWriteWrapperSpec reg
        do! todoWriteWrapperDecodeFailureSpec reg
        let! workspaceDir = mkdtempAsync "tool-execute-after-"
        let! p = plugin (box {| directory = workspaceDir |})
        do! toolExecuteAfterSpec p
        do! rmAsync workspaceDir
        do! abortedRetrySpec ()
        do! fallbackRetryWithoutFrontmatterSpec ()
        do! sessionPostErrorSpec ()
        do! sessionUserQueryPostErrorSpec ()
        do! sessionInterruptedEventSpec ()
        do! repeatedAssistantSpec ()
        do! repeatedIdleBeforeHistoryPersistsNudgeSpec ()
        do! sessionStatusIdleAndSessionIdleDedupSpec ()
        do! sessionStatusBusyDoesNotNudgeSpec ()
        do! opencodeLoopNudgeSpec ()
        do! opencodeBrowserSubsessionHistoryDoesNotLoopNudgeSpec ()
        do! opencodeFreshChatMessageRearmsLoopNudgeSpec ()
        do! reusedSessionSpec ()
        do! muxForceStopTodoNudgeSpec ()
        do! opencodeForceStopTodoNudgeSpec ()
        do! sessionStatusIdleDoesNotTriggerNudgeSpec ()
        do! sessionErrorWithoutFallbackTriggersNudgeSpec ()
        do! nudgeWithoutChatHistoryButEventCarriesTextSpec ()
    }
