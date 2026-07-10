module Wanxiangshu.Tests.TestsEntriesSessionLoop

open Wanxiangshu.Tests.SessionLoopTests
open Wanxiangshu.Tests.TestsTestBody

let sessionLoopTestEntries () : (string * TestBody) list =
    [ "SessionLoopTests.decideAllOpenFallbackFirst", TestBody.Sync(sync decideAllOpenFallbackFirst)
      "SessionLoopTests.decideFallbackClosedTodoOpen", TestBody.Sync(sync decideFallbackClosedTodoOpen)
      "SessionLoopTests.decideOnlyReviewOpen", TestBody.Sync(sync decideOnlyReviewOpen)
      "SessionLoopTests.decideAllClosedResolve", TestBody.Sync(sync decideAllClosedResolve)
      "SessionLoopTests.driveProducesPrioritySequence", TestBody.Sync(sync driveProducesPrioritySequence)
      "SessionLoopTests.driveStopsAfterResolve", TestBody.Sync(sync driveStopsAfterResolve) ]
