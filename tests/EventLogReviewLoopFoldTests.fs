module Wanxiangshu.Tests.EventLogReviewLoopFoldTests

open Wanxiangshu.Kernel.Review.ReviewLoopFold
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
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

    let expected =
        Active
            { task = "ship"
              reviewLoopId = "t"
              currentRound = 1
              latestVerdict = None
              latestFeedback = None }

    equal "activate" expected (foldEvent Inactive e)

let cancelClears () =
    let e = ev eventKindLoopCancelled Map.empty

    let active =
        Active
            { task = "x"
              reviewLoopId = ""
              currentRound = 1
              latestVerdict = None
              latestFeedback = None }

    equal "cancel" Inactive (foldEvent active e)

let acceptClears () =
    let e = ev eventKindReviewVerdict (Map [ "verdict", verdictAccepted ])

    let active =
        Active
            { task = "x"
              reviewLoopId = ""
              currentRound = 1
              latestVerdict = None
              latestFeedback = None }

    equal "accept" Inactive (foldEvent active e)

let needsRevisionKeeps () =
    let e = ev eventKindReviewVerdict (Map [ "verdict", verdictNeedsRevision ])

    let active =
        Active
            { task = "x"
              reviewLoopId = ""
              currentRound = 1
              latestVerdict = None
              latestFeedback = None }

    let expected =
        Active
            { task = "x"
              reviewLoopId = ""
              currentRound = 2
              latestVerdict = Some verdictNeedsRevision
              latestFeedback = None }

    equal "needs_revision" expected (foldEvent active e)

let activeTaskProjection () =
    let active =
        Active
            { task = "t"
              reviewLoopId = ""
              currentRound = 1
              latestVerdict = None
              latestFeedback = None }

    equal "projection" (Some "t") (activeTask active)
    equal "none when inactive" None (activeTask Inactive)

let isLoopActiveFlag () =
    let active =
        Active
            { task = "t"
              reviewLoopId = ""
              currentRound = 1
              latestVerdict = None
              latestFeedback = None }

    checkBare (isLoopActive active)
    checkBare (not (isLoopActive Inactive))

let run () =
    inactiveInitial ()
    activateSetsTask ()
    cancelClears ()
    acceptClears ()
    needsRevisionKeeps ()
    activeTaskProjection ()
    isLoopActiveFlag ()
