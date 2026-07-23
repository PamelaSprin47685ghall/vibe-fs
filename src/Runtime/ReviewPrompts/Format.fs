module Wanxiangshu.Runtime.ReviewPrompts.Format

open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.Prompt
open Wanxiangshu.Kernel.ReviewSession.Types

let submitReviewIsWip (wip: bool option) : bool = defaultArg wip true

let private reviewMode (state: string) : PromptTarget =
    PromptTarget.EvidenceTarget("review_mode", state)

let formatWipAcknowledgment (task: string) : string =
    let docView =
        { objective = task
          background = None
          agentRole = AgentRole.Implementation
          targets =
            [ reviewMode "active"
              PromptTarget.EvidenceTarget("progress", "saved") ]
          boundaries = []
          rules =
            [ PromptRule.Policy "Continue working carefully. Do not stop or wait after this acknowledgment."
              PromptRule.Contract
                  "When every requirement is complete, call submit_review again with wip=false and the full affected-file list." ]
          outcomes =
            [ { label = "review_progress"
                text = "recorded" } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create formatWipAcknowledgment doc: %A" errs

let private recommendationOutcomes (items: string list) : PromptOutcome list =
    items
    |> List.mapi (fun i line ->
        { label = sprintf "recommendation_%d" (i + 1)
          text = line })

let private acceptedDoc (items: string list) : PromptDocumentView =
    { objective = "Carry out the reviewer's final recommendations carefully and completely."
      background = None
      agentRole = AgentRole.Implementation
      targets = [ reviewMode "ended" ]
      boundaries = []
      rules =
        [ PromptRule.Policy
              "Do not treat this as a free pass. Implement every reviewer recommendation before closing the task."
          PromptRule.Policy "Preserve useful lessons in permanent tests or project documentation." ]
      outcomes =
        { label = "follow_through"
          text = "required" }
        :: recommendationOutcomes items }

let private needsRevisionDoc (items: string list) : PromptDocumentView =
    let rules =
        [ PromptRule.Policy "Address every feedback item carefully and continue for as long as needed."
          PromptRule.Policy "Preserve what is correct; fix every listed item before resubmitting." ]

    let outcomes =
        { label = "verdict"
          text = "needs_revision" }
        :: (items
            |> List.mapi (fun i line ->
                { label = sprintf "feedback_%d" (i + 1)
                  text = line }))

    { objective = "Revise the work using the reviewer's structured feedback."
      background = None
      agentRole = AgentRole.Implementation
      targets = [ reviewMode "active" ]
      boundaries = []
      rules = rules
      outcomes = outcomes }

let private terminatedDoc: PromptDocumentView =
    { objective = "Recover from a review that ended without a verdict."
      background = None
      agentRole = AgentRole.Implementation
      targets = [ reviewMode "active" ]
      boundaries = []
      rules = [ PromptRule.Policy "Verify state, resolve blockers, and submit again when ready." ]
      outcomes =
        [ { label = "verdict"
            text = "terminated" } ] }

let formatReviewResult (result: ReviewResult) : string =
    let docView =
        match result with
        | ReviewResult.Accepted items -> acceptedDoc items
        | ReviewResult.NeedsRevision items -> needsRevisionDoc items
        | ReviewResult.Terminated -> terminatedDoc

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create formatReviewResult doc: %A" errs
