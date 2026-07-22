module Wanxiangshu.Runtime.ReviewPrompts.OmpVariant

open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.Prompt
open Wanxiangshu.Runtime.ReviewPrompts.Submission

let reviewerNudgePrompt =
    let docView =
        { objective = "Submit review verdict via return_reviewer now."
          background = None
          agentRole = AgentRole.CodeReview
          targets = [ PromptTarget.EvidenceTarget("reviewer_session", "awaiting_verdict") ]
          boundaries = []
          rules =
            [ PromptRule.Contract "tool: return_reviewer"
              PromptRule.Contract "verdict: PERFECT | REVISE"
              PromptRule.Contract "feedback: required non-empty string for PERFECT and REVISE"
              PromptRule.Policy "Do not explain plans in chat — call the tool immediately."
              PromptRule.Policy "Terminate the reviewer session after the tool call." ]
          outcomes =
            [ { label = "PERFECT"
                text = "Accept with non-empty feedback opinion." }
              { label = "REVISE"
                text = "Request revision with non-empty actionable feedback." } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create reviewerNudgePrompt doc: %A" errs

let reviewInstructionsOmp = Wanxiangshu.Runtime.ReviewPrompts.Instructions.reviewInstructions

let reviewerNudgePromptOmp = reviewerNudgePrompt

/// OMP review child initial prompt: typed PromptDocument only.
let buildOmpReviewInitialPrompt (report: string) (affectedFiles: string list) (task: string option) : string =
    let taskText =
        match task with
        | Some t when not (System.String.IsNullOrWhiteSpace t) -> t
        | _ -> "Perform a code review of the submitted work."

    reviewerPrompt taskText report affectedFiles
