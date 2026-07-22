module Wanxiangshu.Tests.NudgeSnapshotSourceTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Review.ReviewLoopFold
open Wanxiangshu.Kernel.Nudge.NudgeSnapshotSource
open Wanxiangshu.Kernel.Nudge.Types

let workStateRunnerAndLoop () =
    let source =
        { openTodos = []
          lastAssistantText = ""
          skipTodo = false
          skipReview = false
          agentFromMessage = None
          modelFromMessage = None
          reviewLoop =
            Active
                { task = "t"
                  reviewLoopId = ""
                  currentRound = 1
                  latestVerdict = None
                  latestFeedback = None }
          runnerPresence = RunnerPresence.Active
          blockStatus = NudgeBlockStatus.Allowed
          turnId = "" }

    equal "runner+loop" SessionWorkState.RunnerWithLoop (workStateFromSource source)

let run () = workStateRunnerAndLoop ()
