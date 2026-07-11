module Wanxiangshu.Tests.SessionGateDemandTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.SessionGateDemand
open Wanxiangshu.Kernel.SessionLoop

let priorityFallbackFirst () =
    let d =
        resolveFromSignals [ GateSignal.FallbackContinue; GateSignal.TodoNudge; GateSignal.ReviewNudge ]

    equal "fallback wins" SessionGateDemand.FallbackContinue d
    equal "mode" SessionGateMode.FallbackContinue (gateModeFromDemand d)

let priorityTodoOverReview () =
    let d = resolveFromSignals [ GateSignal.TodoNudge; GateSignal.ReviewNudge ]
    equal "todo over review" SessionGateDemand.TodoNudge d

let reviewOnly () =
    let d = resolveFromSignals [ GateSignal.ReviewNudge ]
    equal "review" SessionGateDemand.ReviewNudge d

let settled () =
    let d = resolveFromSignals []
    equal "settled demand" SessionGateDemand.Settled d
    equal "resolve" Resolve (decide (gateModeFromDemand d))

let run () =
    priorityFallbackFirst ()
    priorityTodoOverReview ()
    reviewOnly ()
    settled ()
