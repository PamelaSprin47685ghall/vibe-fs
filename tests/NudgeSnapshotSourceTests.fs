module Wanxiangshu.Tests.NudgeSnapshotSourceTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.EventLog.ReviewLoopFold
open Wanxiangshu.Kernel.Nudge.NudgeSnapshotSource
open Wanxiangshu.Kernel.Nudge.Types

let workStateRunnerAndLoop () =
    let source =
        { openTodos = []
          lastAssistantText = ""
          agentFromMessage = None
          modelFromMessage = None
          reviewLoop = Active "t"
          runnerPresence = RunnerPresence.Active
          blockStatus = NudgeBlockStatus.Allowed
          turnId = "" }

    equal "runner+loop" SessionWorkState.RunnerWithLoop (workStateFromSource source)

let run () = workStateRunnerAndLoop ()
