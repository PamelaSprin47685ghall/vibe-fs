module Wanxiangshu.Tests.TestsEntriesSessionLoop

open Wanxiangshu.Tests.FallbackRuntimeFlagsTests
open Wanxiangshu.Tests.NudgeSnapshotSourceTests
open Wanxiangshu.Tests.NudgeWorkStateTests
open Wanxiangshu.Tests.SessionGateDemandTests
open Wanxiangshu.Tests.SessionLoopTests
open Wanxiangshu.Tests.ToolExecutionStatusTests
open Wanxiangshu.Tests.TestsTestBody

let sessionLoopTestEntries () : (string * TestBody) list =
    [ "SessionGateDemandTests.run", TestBody.Sync SessionGateDemandTests.run
      "FallbackRuntimeFlagsTests.run", TestBody.Sync FallbackRuntimeFlagsTests.run
      "NudgeSnapshotSourceTests.run", TestBody.Sync NudgeSnapshotSourceTests.run
      "NudgeWorkStateTests.run", TestBody.Sync NudgeWorkStateTests.run
      "ToolExecutionStatusTests.run", TestBody.Sync ToolExecutionStatusTests.run
      "SessionLoopTests.decideAllOpenFallbackFirst", TestBody.Sync(sync decideAllOpenFallbackFirst)
      "SessionLoopTests.decideFallbackClosedTodoOpen", TestBody.Sync(sync decideFallbackClosedTodoOpen)
      "SessionLoopTests.decideOnlyReviewOpen", TestBody.Sync(sync decideOnlyReviewOpen)
      "SessionLoopTests.decideAllClosedResolve", TestBody.Sync(sync decideAllClosedResolve)
      "SessionLoopTests.driveProducesPrioritySequence", TestBody.Sync(sync driveProducesPrioritySequence)
      "SessionLoopTests.driveStopsAfterResolve", TestBody.Sync(sync driveStopsAfterResolve) ]
