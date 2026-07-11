module Wanxiangshu.Tests.EventLogReviewLoopFoldTests

open Wanxiangshu.Kernel.EventLog.ReviewLoopFold
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Tests.Assert

let private ev kind payload =
    { V = 1
      Session = "s1"
      Kind = kind
      At = "t"
      Payload = payload }

let inactiveInitial () =
    equal "initial inactive" Inactive initial

let activateSetsTask () =
    let e = ev eventKindLoopActivated (Map [ "task", "ship" ])
    equal "activate" (Active "ship") (foldEvent Inactive e)

let cancelClears () =
    let e = ev eventKindLoopCancelled Map.empty
    equal "cancel" Inactive (foldEvent (Active "x") e)

let acceptClears () =
    let e = ev eventKindReviewVerdict (Map [ "verdict", verdictAccepted ])
    equal "accept" Inactive (foldEvent (Active "x") e)

let needsRevisionKeeps () =
    let e = ev eventKindReviewVerdict (Map [ "verdict", verdictNeedsRevision ])
    equal "needs_revision" (Active "x") (foldEvent (Active "x") e)

let activeTaskProjection () =
    equal "projection" (Some "t") (activeTask (Active "t"))
    equal "none when inactive" None (activeTask Inactive)

let isLoopActiveFlag () =
    checkBare (isLoopActive (Active "t"))
    checkBare (not (isLoopActive Inactive))

let run () =
    inactiveInitial ()
    activateSetsTask ()
    cancelClears ()
    acceptClears ()
    needsRevisionKeeps ()
    activeTaskProjection ()
    isLoopActiveFlag ()
