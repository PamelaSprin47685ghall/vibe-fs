module Wanxiangshu.Kernel.Nudge.SubmitReviewHooks

let isSubmitReviewToolName (name: string) : bool =
    if isNull name then
        false
    else
        name.Trim().Equals("submit_review", System.StringComparison.OrdinalIgnoreCase)

let submitReviewWipToolClearsNudgeDedup (toolName: string) (_outputText: string) : bool =
    isSubmitReviewToolName toolName
