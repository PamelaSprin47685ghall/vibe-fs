module Wanxiangshu.Tests.AgentNudgeSpecsWip

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Runtime.Nudge.NudgeDerivation
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.NudgeProjection
open Wanxiangshu.Kernel.Nudge.Types

let private snap todos msg blocked agent : Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot =
    { todos = todos
      lastAssistantMessage = msg
      workState = getSessionWorkState false false todos
      blockStatus =
        (if blocked then
             NudgeBlockStatus.Blocked
         else
             NudgeBlockStatus.Allowed)
      nudgeAnchorKey = msg
      agentFromMessage = agent
      modelFromMessage = None
      reviewLoop = None
      humanTurnId = None }

let private ev session kind payload =
    { V = 1
      Session = session
      Kind = kind
      At = ""
      Payload = payload
      EventId = None
      WriterId = None
      Sequence = None
      Checksum = None }

let foldNudgeDedupBlocksSameAnchor () =
    let events =
        [ ev "s1" eventKindNudgeDispatched (Map [ "action", "nudge-todo"; "anchor", "t1\u001eworking" ]) ]

    let st = NudgeProjection.foldDedupStream "s1" events
    check "same anchor blocked" (NudgeProjection.isBlocked st "t1\u001eworking")
    check "new anchor open" (not (NudgeProjection.isBlocked st "t2\u001eworking"))

let foldWipClearsBlock () =
    let events =
        [ ev "s1" eventKindNudgeDispatched (Map [ "action", "nudge-loop"; "anchor", "a" ])
          ev "s1" eventKindSubmitReviewWipRecorded Map.empty ]

    let st = NudgeProjection.foldDedupStream "s1" events
    check "wip clears dedup" (not (NudgeProjection.isBlocked st "a"))

let decideNudgeWipAllowsAfterClear () =
    let snapStillNudged = snap [] "still implementing" true None

    let d =
        deriveAction
            { snapStillNudged with
                workState = getSessionWorkState false true snapStillNudged.todos }

    equal "integral blocked -> NudgeNone" NudgeNone d

let submitReviewWipNudgeDedup () =
    foldNudgeDedupBlocksSameAnchor ()
    foldWipClearsBlock ()
    decideNudgeWipAllowsAfterClear ()
