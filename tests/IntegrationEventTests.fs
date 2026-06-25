module VibeFs.Tests.IntegrationEventTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.IntegrationEventTestsMux
open VibeFs.Tests.IntegrationEventTestsMuxWrappers
open VibeFs.Tests.IntegrationEventTestsOpencode
open VibeFs.Tests.TempWorkspace
open VibeFs.Mux.Plugin
open VibeFs.Opencode.Plugin
open VibeFs.Shell.Dyn

let eventHookSpec (reg: obj) = promise {
    let hook = get reg "eventHook"
    check "eventHook.length === 2" (unbox<int> (hook?length) = 2)
    let ehResult = hook $ (createObj [ "type", box "stream-abort"; "workspaceId", box "test-ws" ], null)
    check "eventHook returns Promise" (not (isNullish (get ehResult "then")))
    do! unbox<JS.Promise<unit>> ehResult
}

let run () : JS.Promise<unit> =
    promise {
        let reg = createRegistration (createObj [])
        do! eventHookSpec reg
        do! repeatedTodoNudgeSpec ()
        do! reviewerRejectRenudgesLoopSpec ()
        do! muxSubmitReviewWipDoesNotSuppressLoopNudgeSpec ()
        do! syntaxWrapperSpec reg
        do! todoWriteWrapperSpec reg
        do! todoWriteWrapperDecodeFailureSpec reg
        let! workspaceDir = mkdtempAsync "tool-execute-after-"
        let! p = plugin (box {| directory = workspaceDir |})
        do! toolExecuteAfterSpec p
        do! rmAsync workspaceDir
        do! abortedRetrySpec ()
        do! repeatedAssistantSpec ()
        do! opencodeLoopNudgeSpec ()
        do! opencodeFreshChatMessageRearmsLoopNudgeSpec ()
        do! reusedSessionSpec ()
    }