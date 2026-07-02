module Wanxiangshu.Tests.AgentNudgeSpecs

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeDerivation
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.Nudge.SubmitReviewHooks
open Wanxiangshu.Kernel.ReviewPrompts

let private snap todos msg alreadyNudged agent isLoop : Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot =
    { todos = todos; lastAssistantMessage = msg; isLoopActive = isLoop
      alreadyNudged = alreadyNudged; agentFromMessage = agent
      lastAssistantIsCompaction = false; anchorPromptIssued = false
      hasActiveRunner = false }

let decision () =
    equal "todos -> NudgeTodo" NudgeTodo (deriveAction (snap [ "a" ] "working" false None false) None None)
    equal "todos+question -> None" NudgeNone (deriveAction (snap [ "a" ] "what now?" false None false) None None)
    equal "todos+skip -> None" NudgeNone (deriveAction (snap [ "a" ] "done <skip-todo-check />" false None false) None None)
    equal "nothing -> None" NudgeNone (deriveAction (snap [] "ok" false None false) None None)
    equal "loop -> NudgeLoop" NudgeLoop (deriveAction (snap [] "ok" false None true) None None)
    equal "loop+skip -> None" NudgeNone (deriveAction (snap [] "done <skip-loop-check />" false None true) None None)

let dedup () =
    let s = snap [ "a" ] "working" false None false
    equal "first -> NudgeTodo" NudgeTodo (deriveAction s None None)
    equal "same action suppressed" NudgeNone (deriveAction s (Some NudgeTodo) (Some "working"))
    let s3 = snap [] "done" false None false
    equal "cleared context resets" NudgeNone (deriveAction s3 (Some NudgeTodo) (Some "done"))

let alreadyNudgedFromTailTexts' () =
    check "tail loop nudge only -> true" (deriveAlreadyNudged [ loopNudgePrompt ])
    check "tail wip ack only -> false" (not (deriveAlreadyNudged [ submitReviewWipAcknowledgment ]))
    check "loop nudge then wip ack -> false"
        (not (deriveAlreadyNudged [ loopNudgePrompt; submitReviewWipAcknowledgment ]))
    check "todo nudge tail -> true" (deriveAlreadyNudged [ todoNudgePrompt ])
    check "empty tail -> false" (not (deriveAlreadyNudged []))

let decideNudge' () =
    match deriveAction (snap [ "a" ] "working" false None false) None None with
    | NudgeTodo -> check "unclaimed fresh history nudges todo" true
    | _ -> check "unclaimed fresh history nudges todo" false

    match deriveAction (snap [ "a" ] "working" true None false) None None with
    | NudgeNone -> check "already-nudged stop -> StandDown" true
    | _ -> check "already-nudged stop -> StandDown" false

    match deriveAction (snap [] "done" false None false) None None with
    | NudgeNone -> check "no work -> StandDown" true
    | _ -> check "no work -> StandDown" false

    match deriveAction (snap [] "ok" false None true) None None with
    | NudgeLoop -> check "loop active nudges loop" true
    | _ -> check "loop active nudges loop" false

let submitReviewWipNudgeDedup () =
    alreadyNudgedFromTailTexts' ()

let run () =
    decision ()
    dedup ()
    alreadyNudgedFromTailTexts' ()
    decideNudge' ()
