module Wanxiangshu.Tests.AgentNudgeSpecsWip

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeDerivation
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.EventLog.Fold
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
      modelFromMessage = None }

let private ev session kind payload =
    { V = 1
      Session = session
      Kind = kind
      At = ""
      Payload = payload }

let foldNudgeDedupBlocksSameAnchor () =
    let events =
        [ ev "s1" eventKindNudgeDispatched (Map [ "action", "nudge-todo"; "anchor", "t1\u001eworking" ]) ]

    let st = foldNudgeDedup "s1" events
    check "same anchor blocked" (isNudgeBlockedForAnchor st "t1\u001eworking")
    check "new anchor open" (not (isNudgeBlockedForAnchor st "t2\u001eworking"))

let foldWipClearsBlock () =
    let events =
        [ ev "s1" eventKindNudgeDispatched (Map [ "action", "nudge-loop"; "anchor", "a" ])
          ev "s1" eventKindSubmitReviewWipRecorded Map.empty ]

    let st = foldNudgeDedup "s1" events
    check "wip clears dedup" (not (isNudgeBlockedForAnchor st "a"))

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
