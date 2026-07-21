module Wanxiangshu.Tests.NudgeWorkStateTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Nudge.Types

let axesIdle () =
    equal "idle" SessionWorkState.Idle (workStateFromAxes false false [])

let axesRunnerOnly () =
    equal "runner" SessionWorkState.RunnerOnly (workStateFromAxes true false [])

let axesLoopWithTodos () =
    equal "loop+todos" SessionWorkState.LoopWithTodos (workStateFromAxes false true [ "t" ])

let axesAll () =
    equal "all" SessionWorkState.AllAxes (workStateFromAxes true true [ "x" ])

let helpersMatchAxes () =
    let s = SessionWorkState.RunnerWithLoop
    check "runner" (hasActiveRunner s)
    check "loop" (isLoopActiveWorkState s)
    check "no todos" (not (hasOpenTodos s))

let run () =
    axesIdle ()
    axesRunnerOnly ()
    axesLoopWithTodos ()
    axesAll ()
    helpersMatchAxes ()
