module VibeFs.Kernel.PlanCritique

open VibeFs.Kernel.PlanCommon
open VibeFs.Kernel.PlanTypes

type private ResultBuilder() =
    member _.Bind(m, f) = Result.bind f m
    member _.Return(x) = Ok x

let private result = ResultBuilder()

let buildPlanCritiqueToolSchema : PlanToolSchema =
    { name = "submit_plan_critique"
      description = "Submit a critique of a plan branch."
      parameters =
          mkSchema
              [ "branchId", strProp "Branch id."
                "critiqueMarkdown", strProp "Full critique markdown."
                "criticalIssues", strArrayProp "Critical issues."
                "missingRequirements", strArrayProp "Missing requirements."
                "counterexamples", strArrayProp "Counterexamples."
                "improvementDirections", strArrayProp "Improvement directions." ]
              [ "branchId"; "critiqueMarkdown"; "criticalIssues"; "missingRequirements"; "counterexamples"; "improvementDirections" ] }

let parsePlanCritiqueToolCall (arguments: obj) : Result<PlanBranchCritique, string> =
    result {
        let! branchId = optString arguments "branchId"
        let! critiqueMarkdown = optString arguments "critiqueMarkdown"
        let! criticalIssues = optStringArray arguments "criticalIssues"
        let! missingRequirements = optStringArray arguments "missingRequirements"
        let! counterexamples = optStringArray arguments "counterexamples"
        let! improvementDirections = optStringArray arguments "improvementDirections"
        return
            { branchId = branchId
              critiqueMarkdown = critiqueMarkdown
              criticalIssues = criticalIssues
              missingRequirements = missingRequirements
              counterexamples = counterexamples
              improvementDirections = improvementDirections }
    }

let buildPlanCritiquePrompt (_: PlanRequest) (c: PlanBranchCandidate) : string =
    "You are a ruthless critic reviewing a plan draft. Identify concrete problems, missing requirements, unstated assumptions, and ways the plan could fail. Do NOT propose fixes; only diagnose."
    + "\n\nBranch: " + c.branchId + " (lens " + lensName c.lens + ")"
    + "\n\nPlan:\n" + c.candidatePlanMarkdown
    + "\n\nSummary: " + c.candidatePlanSummary
    + "\nSelf-critique: " + c.selfCritique
    + "\n\nCall the submit_plan_critique tool with your diagnosis. All required fields must be present and non-empty."
