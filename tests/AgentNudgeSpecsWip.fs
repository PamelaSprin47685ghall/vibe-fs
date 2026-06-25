module VibeFs.Tests.AgentNudgeSpecsWip

open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.NudgeState
open VibeFs.Kernel.Nudge.Types
open VibeFs.Kernel.PromptFragments
open VibeFs.Kernel.ReviewPrompts
open VibeFs.Shell.OpencodeSessionEventCodec

let private snapshot todos msg alreadyNudged agent : SessionSnapshot =
    { todos = todos; lastAssistantMessage = msg; alreadyNudged = alreadyNudged; agentFromMessage = agent }

let private noReview (_: string) = false
let private noChild (_: string) = None

let alreadyNudgedFromTailTexts' () =
    check "tail loop nudge only → true" (alreadyNudgedFromTailTexts [ loopNudgePrompt ])
    check "tail wip ack only → false" (not (alreadyNudgedFromTailTexts [ submitReviewWipAcknowledgment ]))
    check "loop nudge then wip ack → false"
        (not (alreadyNudgedFromTailTexts [ loopNudgePrompt; submitReviewWipAcknowledgment ]))
    check "todo nudge tail → true" (alreadyNudgedFromTailTexts [ todoNudgePrompt ])
    check "empty tail → false" (not (alreadyNudgedFromTailTexts []))

let submitReviewWipToolClearsNudgeDedup' () =
    check "submit_review wip output clears" (submitReviewWipToolClearsNudgeDedup "submit_review" submitReviewWipAcknowledgment)
    check "other tool does not clear" (not (submitReviewWipToolClearsNudgeDedup "read" submitReviewWipAcknowledgment))
    check "submit_review non-wip output does not clear"
        (not (submitReviewWipToolClearsNudgeDedup "submit_review" "Review passed."))

let decideNudgeWipNeutralAlreadyNudged' () =
    let loopReview (_: string) = true
    let claimed, _ = tryClaimNudge emptyState "s"
    let snap =
        snapshot [] "still implementing" false None
        |> fun s -> { s with alreadyNudged = false }
    match snd (decideNudge loopReview noChild claimed "s" snap) with
    | Send(text, _) -> check "wip-neutral snapshot allows loop nudge" (text = loopNudgePrompt)
    | StandDown -> check "wip-neutral snapshot allows loop nudge" false

    let snapStillNudged = snapshot [] "still implementing" true None
    let _, d = decideNudge loopReview noChild claimed "s" snapStillNudged
    equal "history still nudged → StandDown" StandDown d

let submitReviewWipNudgeDedup () =
    alreadyNudgedFromTailTexts' ()
    submitReviewWipToolClearsNudgeDedup' ()
    decideNudgeWipNeutralAlreadyNudged' ()
