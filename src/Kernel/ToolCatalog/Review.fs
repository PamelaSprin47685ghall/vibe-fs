module Wanxiangshu.Kernel.ToolCatalog.Review

open Wanxiangshu.Kernel.ToolCatalog.ToolSpec

let internal submitReviewSpec: ToolSpec =
    { name = "submit_review"
      description =
        "Submit completed work for review. Creates a reviewer that examines the changes against evaluation criteria and returns PERFECT or actionable feedback. Only works when session is in active With-Review Mode."
      paramDocs =
        map
            [ "report", "Detailed report of what was done"
              "affectedFiles", "List of file paths that were modified or created"
              "wip",
              "Optional, defaults to true when omitted. true records partial progress; false asserts full completion." ]
      requiredFields = [ "report"; "affectedFiles" ] }

let internal returnReviewerSpec: ToolSpec =
    { name = "return_reviewer"
      description =
        "Submit your review verdict via tool args only. Both PERFECT and REVISE require non-empty feedback (review opinion). Do not put the verdict only in chat text."
      paramDocs =
        map
            [ "verdict", "PERFECT to accept, REVISE to request revision"
              "feedback",
              "Required non-empty review opinion for both PERFECT and REVISE. Sole source of review feedback; conversation prose is ignored." ]
      requiredFields = [ "verdict"; "feedback" ] }
