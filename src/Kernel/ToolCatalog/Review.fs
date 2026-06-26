module VibeFs.Kernel.ToolCatalog.Review

open VibeFs.Kernel.ToolCatalog.ToolSpec

let internal submitReviewSpec: ToolSpec =
    { name = "submit_review"
      description =
        "Submit completed work for review. Creates a reviewer that examines the changes against evaluation criteria and returns PASS or actionable feedback. Only works when session is in active With-Review Mode."
      paramDocs =
        map
            [ "report", "Detailed report of what was done"
              "affectedFiles", "List of file paths that were modified or created"
              "wip",
               "Optional. Defaults to true when omitted. true means this submission is partial — the task is not fully complete yet. false means you assert the full task is complete in this submission." ]
      requiredFields = [ "report"; "affectedFiles" ] }

let internal returnReviewerSpec: ToolSpec =
    { name = "return_reviewer"
      description = "Submit your review verdict."
      paramDocs =
        map
            [ "verdict", "PASS to accept, REJECT to reject"
              "feedback", "Detailed, actionable feedback when rejecting; omit when passing" ]
      requiredFields = [ "verdict" ] }
