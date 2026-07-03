module Wanxiangshu.Kernel.Nudge.SubmitReviewHooks

open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.ReviewPrompts

let isSubmitReviewWipProgressOutput (text: string) : bool =
    text.Trim() = submitReviewWipAcknowledgment

let isSubmitReviewToolName (name: string) : bool =
    name.Trim().Equals("submit_review", System.StringComparison.OrdinalIgnoreCase)

let submitReviewWipToolClearsNudgeDedup (toolName: string) (outputText: string) : bool =
    isSubmitReviewToolName toolName && isSubmitReviewWipProgressOutput outputText
