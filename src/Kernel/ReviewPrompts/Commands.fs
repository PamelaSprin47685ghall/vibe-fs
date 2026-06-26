module VibeFs.Kernel.ReviewPrompts.Commands

open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.PromptFrontMatter
open VibeFs.Kernel.PromptFragments

let withReviewCommandTemplate =
    frontMatterPrompt
        [ yamlField commandField commandWithReview
          yamlField taskField "$ARGUMENTS" ]
        (String.concat
            "\n"
            [ "You are entering With-Review Mode."
              "Complete the task recorded in the front matter."
              ""
              "The reviewer will judge your eventual submission using these criteria:"
              ""
              reviewCriteria
              ""
              "Before finishing, you must call submit_review with:"
              "- report: a detailed description of what you did and why"
              "- affectedFiles: every file you modified or created"
              "- wip: omit or true until the task is fully complete; false only when everything required is done"
              "Defend proactively against reviewer rejection: keep the implementation natural, minimal, complete, and well-tested."
              "Do not end the conversation without submit_review." ])

let withReviewPrecheckCommandTemplate =
    frontMatterPrompt
        [ yamlField commandField commandWithReviewPrecheck
          yamlField taskField "$ARGUMENTS" ]
        (String.concat
            "\n"
            [ "You are requesting With-Review Mode with pre-review."
              "The task recorded in the front matter will be pre-reviewed first."
              ""
              "If the task is activated, the reviewer will later judge your submission using these criteria:"
              ""
              reviewCriteria
              ""
              "If activated, complete the task and later submit your work via submit_review."
              "Do not treat this message itself as completed work." ])
