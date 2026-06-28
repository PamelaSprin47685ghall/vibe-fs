module Wanxiangshu.Tests.FallbackEventBridgeToolTextTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackEventBridge
open Wanxiangshu.Tests.FallbackEventBridgeTests

let private mkToolCallTextMsg () : obj =
    let parts : obj array = [|
        box (createObj [ "type", box "text"; "text", box "<tool_call>\n<function=edit>{\"path\":\"a.fs\"}</function>\n</tool_call>" ])
    |]
    box (createObj [
        "info", box (createObj [ "role", box "assistant" ])
        "parts", box parts
    ])

let handleEvent_toolText_belowMaxContinues_continuesSame () = promise {
    let model1 = mkModel "a" "m1"
    let chain  = [ model1; mkModel "b" "m2" ]
    let cfg    = { (mkConfig ()) with LoopMaxContinues = 3 }
    let rt     = FallbackRuntimeState()
    let sid    = "sess-tt-below"
    rt.SetChain sid chain
    rt.SetAgentName sid "reviewer"

    let s0 = rt.GetOrCreateState sid
    rt.UpdateState sid { s0 with Phase = FallbackPhase.Idle; ContinueCount = 0 }

    let translator = FakeTranslator(sid, FallbackEvent.SessionIdle) :> IEventTranslator
    let executor   = FakeExecutor(messages = [| mkToolCallTextMsg () |])

    let! result = handleEvent translator rt (fun _ -> cfg) executor (box ())

    equal "same model currentIndex" 0 result.State.CurrentIndex
    equal "continueCount incremented" 1 result.State.ContinueCount
    equal "one continue call" 1 executor.ContinueCalls.Length
    equal "continue used model1" "m1" (snd executor.ContinueCalls.Head).ModelID
    equal "no abort" 0 executor.AbortCalls.Length
}

let handleEvent_toolText_afterMaxContinues_switchesModel () = promise {
    let model1 = mkModel "a" "m1"
    let model2 = mkModel "b" "m2"
    let chain  = [ model1; model2 ]
    let cfg    = { (mkConfig ()) with LoopMaxContinues = 2 }
    let rt     = FallbackRuntimeState()
    let sid    = "sess-tt-switch"
    rt.SetChain sid chain
    rt.SetAgentName sid "reviewer"

    let s0 = rt.GetOrCreateState sid
    rt.UpdateState sid { s0 with Phase = FallbackPhase.Idle; ContinueCount = 1 }

    let translator = FakeTranslator(sid, FallbackEvent.SessionIdle) :> IEventTranslator
    let executor   = FakeExecutor(messages = [| mkToolCallTextMsg () |])

    let! result = handleEvent translator rt (fun _ -> cfg) executor (box ())

    equal "currentIndex advanced to 1" 1 result.State.CurrentIndex
    equal "continueCount reset" 0 result.State.ContinueCount
    equal "abort called once" 1 executor.AbortCalls.Length
    equal "one continue call" 1 executor.ContinueCalls.Length
    equal "continue used model2" "m2" (snd executor.ContinueCalls.Head).ModelID
}

let handleEvent_toolText_afterMaxContinues_chainEnd_propagates () = promise {
    let model1 = mkModel "a" "m1"
    let chain  = [ model1 ]
    let cfg    = { (mkConfig ()) with LoopMaxContinues = 2 }
    let rt     = FallbackRuntimeState()
    let sid    = "sess-tt-end"
    rt.SetChain sid chain
    rt.SetAgentName sid "reviewer"

    let s0 = rt.GetOrCreateState sid
    rt.UpdateState sid { s0 with Phase = FallbackPhase.Idle; ContinueCount = 1 }

    let translator = FakeTranslator(sid, FallbackEvent.SessionIdle) :> IEventTranslator
    let executor   = FakeExecutor(messages = [| mkToolCallTextMsg () |])

    let! result = handleEvent translator rt (fun _ -> cfg) executor (box ())

    equal "phase Exhausted" FallbackPhase.Exhausted result.State.Phase
    equal "propagate called" 1 executor.PropagateCalls.Length
}

let run () = promise {
    do! handleEvent_toolText_belowMaxContinues_continuesSame ()
    do! handleEvent_toolText_afterMaxContinues_switchesModel ()
    do! handleEvent_toolText_afterMaxContinues_chainEnd_propagates ()
}
