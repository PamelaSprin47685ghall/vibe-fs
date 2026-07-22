module Wanxiangshu.Tests.PromptTomlTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.Prompt

let private renderBasicDocument () =
    let view: PromptDocumentView =
        { objective = "Ship the feature"
          background = Some "Context for the work."
          agentRole = AgentRole.Implementation
          targets = [ FileReference "src/A.fs" ]
          boundaries = []
          rules = [ PromptRule.Policy "Prefer small diffs." ]
          outcomes = [ { label = "done"; text = "feature shipped" } ] }

    match PromptDocument.create view with
    | Error errs -> check ("prompt document create: " + string errs) false
    | Ok doc ->
        let text = PromptToml.render doc
        check "prompt has objective" (text.Contains "objective")
        check "prompt has agent_role" (text.Contains "agent_role" || text.Contains "Implementation")
        check "prompt embeds objective text" (text.Contains "Ship the feature")
        check "prompt embeds outcome" (text.Contains "feature shipped")
        check "prompt is not yaml front matter" (not (text.StartsWith "---"))

let private renderAcceptedFollowThroughShape () =
    let text =
        Wanxiangshu.Runtime.ReviewPrompts.Format.formatReviewResult (
            Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Accepted [ "tighten tests" ]
        )

    check "accepted omits PERFECT" (not (text.Contains "PERFECT"))
    check "accepted omits Review passed" (not (text.Contains "Review passed"))
    check "accepted requires follow_through" (text.Contains "follow_through" || text.Contains "recommendations")
    check "accepted embeds opinion" (text.Contains "tighten tests")
    check "accepted no markdown divider" (not (text.Contains "\n---\n") && not (text.Contains "=== "))

let private renderNeedsRevisionStructured () =
    let text =
        Wanxiangshu.Runtime.ReviewPrompts.Format.formatReviewResult (
            Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.NeedsRevision [ "missing tests" ]
        )

    check "revise has verdict outcome" (text.Contains "needs_revision")
    check "revise embeds feedback" (text.Contains "missing tests")
    check "revise has rules/outcomes structure" (text.Contains "rules" || text.Contains "outcomes" || text.Contains "criterion")

let run () : unit =
    renderBasicDocument ()
    renderAcceptedFollowThroughShape ()
    renderNeedsRevisionStructured ()
