module Wanxiangshu.Runtime.ReviewPrompts.Commands

open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.Prompt
open Wanxiangshu.Runtime.PromptFragments

let withReviewCommandTemplate: string =
    let docView =
        { objective = "$ARGUMENTS"
          background = None
          agentRole = AgentRole.CodeReview
          targets = [ PromptTarget.EvidenceTarget("review_mode", "activating") ]
          boundaries = []
          rules =
            reviewCriteriaRules
            @ [ PromptRule.Policy "NO_SHORTCUTS: fully satisfy every task requirement; no partial or deferred work."
                PromptRule.Policy "Defend proactively: natural, minimal, complete, well-tested implementation."
                PromptRule.Contract "Do not end the conversation without submit_review." ]
          outcomes =
            [ { label = "submit_review"
                text =
                  "Before finishing, call submit_review with report, affectedFiles, and wip (true until everything required is done)." }
              { label = "activate"
                text = "With-Review Mode active for the objective task." } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create withReviewCommandTemplate doc: %A" errs
