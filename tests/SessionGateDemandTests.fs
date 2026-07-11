module Wanxiangshu.Tests.SessionGateDemandTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.SessionGateDemand
open Wanxiangshu.Kernel.SessionLoop

let priorityFallbackFirst () =
    let d = resolveGateDemand true true true
    equal "fallback wins" SessionGateDemand.FallbackContinue d
    equal "mode" SessionGateMode.FallbackContinue (gateModeFromDemand d)

let priorityTodoOverReview () =
    let d = resolveGateDemand false true true
    equal "todo over review" SessionGateDemand.TodoNudge d

let reviewOnly () =
    let d = resolveGateDemand false false true
    equal "review" SessionGateDemand.ReviewNudge d

let settled () =
    let d = resolveGateDemand false false false
    equal "settled demand" SessionGateDemand.Settled d
    equal "resolve" Resolve (decide (gateModeFromDemand d))

let run () =
    priorityFallbackFirst ()
    priorityTodoOverReview ()
    reviewOnly ()
    settled ()
