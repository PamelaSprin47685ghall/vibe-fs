module VibeFs.Kernel.ReviewerLoop

open VibeFs.Kernel.ReviewSession

/// What happened in a single reviewer round.
type RoundOutcome =
    | Resolved of result: ReviewResult
    | PromptFailed
    | NoResult

/// What the orchestrator should do after a round: stop with a result, or nudge.
type LoopDecision =
    | Finish of result: ReviewResult
    | Nudge of nudgeCount: int

/// Decide the next move after a round.  Resolved ends the loop; a failed prompt
/// terminates; running out of nudges terminates; otherwise nudge and retry.
let decideAfterRound (nudgeCount: int) (outcome: RoundOutcome) (maxNudges: int) : LoopDecision =
    match outcome with
    | Resolved result -> Finish result
    | PromptFailed -> Finish Terminated
    | NoResult ->
        let next = nudgeCount + 1
        if next >= maxNudges then Finish Terminated else Nudge next

/// Initial round uses the task prompt; every retry uses the short nudge prompt.
let promptParts (nudgeCount: int) (initialParts: string list) (nudgePrompt: string) : string list =
    if nudgeCount = 0 then initialParts else [ nudgePrompt ]
