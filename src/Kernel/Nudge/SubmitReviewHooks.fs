module Wanxiangshu.Kernel.Nudge.SubmitReviewHooks

open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.ReviewPrompts

let isSubmitReviewWipProgressOutput (text: string) : bool =
    text.Trim() = submitReviewWipAcknowledgment

let isSubmitReviewToolName (name: string) : bool =
    name.Trim().Equals("submit_review", System.StringComparison.OrdinalIgnoreCase)

let submitReviewWipToolClearsNudgeDedup (toolName: string) (outputText: string) : bool =
    isSubmitReviewToolName toolName && isSubmitReviewWipProgressOutput outputText

/// Tail message texts in chronological order; wip ack clears a prior nudge marker.
let alreadyNudgedFromTailTexts (texts: string list) : bool =
    List.fold
        (fun nudged text ->
            if isSubmitReviewWipProgressOutput text then false
            elif isNudgePrompt text then true
            else nudged)
        false
        texts
