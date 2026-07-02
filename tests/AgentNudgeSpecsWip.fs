module Wanxiangshu.Tests.AgentNudgeSpecsWip

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeDerivation
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Kernel.Nudge.SubmitReviewHooks

let private snap todos msg alreadyNudged agent : Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot =
    { todos = todos; lastAssistantMessage = msg; isLoopActive = false
      alreadyNudged = alreadyNudged; agentFromMessage = agent
      lastAssistantIsCompaction = false; anchorPromptIssued = false
      hasActiveRunner = false }

let alreadyNudgedFromTailTexts' () =
    check "tail loop nudge only -> true" (deriveAlreadyNudged [ loopNudgePrompt ])
    check "tail wip ack only -> false" (not (deriveAlreadyNudged [ submitReviewWipAcknowledgment ]))
    check "loop nudge then wip ack -> false when wip ack is last"
        (not (deriveAlreadyNudged [ loopNudgePrompt; submitReviewWipAcknowledgment ]))
    check "only last tail text counts: nudge buried under user reply -> false"
        (not (deriveAlreadyNudged [ loopNudgePrompt; "user continued" ]))
    check "todo nudge tail -> true" (deriveAlreadyNudged [ todoNudgePrompt ])
    check "empty tail -> false" (not (deriveAlreadyNudged []))

let submitReviewWipToolClearsNudgeDedup' () =
    check "submit_review wip output clears" (submitReviewWipToolClearsNudgeDedup "submit_review" submitReviewWipAcknowledgment)
    check "other tool does not clear" (not (submitReviewWipToolClearsNudgeDedup "read" submitReviewWipAcknowledgment))
    check "submit_review non-wip output does not clear"
        (not (submitReviewWipToolClearsNudgeDedup "submit_review" "Review passed."))

let decideNudgeWipNeutralAlreadyNudged' () =
    let snapStillNudged = snap [] "still implementing" true None
    let d = deriveAction { snapStillNudged with isLoopActive = true } None None
    equal "history still nudged -> NudgeNone" NudgeNone d

let submitReviewWipNudgeDedup () =
    alreadyNudgedFromTailTexts' ()
    submitReviewWipToolClearsNudgeDedup' ()
    decideNudgeWipNeutralAlreadyNudged' ()
