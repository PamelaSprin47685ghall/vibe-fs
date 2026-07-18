module Wanxiangshu.Tests.FallbackEventBridgeReviewInProgressTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Runtime.Fallback.Coordinator
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Tests.FallbackEventBridgeStateTests

let handleEvent_sessionIdle_emptyOutput_pendingReview_skipsContinue () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let chain = [ model ]
        let rt = FallbackRuntimeStore()
        let sid = "reviewer-child"
        rt.SetChain sid chain
        rt.SetAgentName sid "reviewer"

        let emptyAssistant =
            createObj [ "info", box (createObj [ "role", box "assistant" ]); "parts", box [||] ]

        let translator = FakeTranslator(sid, FallbackEvent.SessionIdle) :> IEventTranslator
        let executor = FakeExecutor(messages = [| emptyAssistant |])

        let! result, _ = handleEvent translator rt defaultCfgLookup executor "" (box ()) (Some(fun s -> s = sid))

        equal "no continue when pending review" 0 (executor.ContinueCalls.Length)
        equal "not consumed" false result.Consumed
    }

let run () =
    promise { do! handleEvent_sessionIdle_emptyOutput_pendingReview_skipsContinue () }
