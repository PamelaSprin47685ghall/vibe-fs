module VibeFs.Kernel.PlanBranches

open Fable.Core
open VibeFs.Kernel.PlanTypes
open VibeFs.Kernel.PlanCommon
open VibeFs.Kernel.PlanHypotheses

type private ResultBuilder() =
    member _.Bind(m, f) = Result.bind f m
    member _.Return(x) = Ok x

let private result = ResultBuilder()

let buildPlanBranchToolSchema : PlanToolSchema =
    { name = "submit_plan_branch"
      description = "Submit the plan branch draft."
      parameters =
          mkSchema
              [ "branchId", strProp "Stable branch id."
                "lens", strProp "Lens name."
                "title", strProp "Short title."
                "candidatePlanMarkdown", strProp "Full plan draft markdown."
                "candidatePlanSummary", strProp "One-line summary."
                "keyAssumptions", strArrayProp "Key assumptions."
                "keyRisks", strArrayProp "Key risks."
                "validationChecks", strArrayProp "Validation checks."
                "selfCritique", strProp "Self critique."
                "confidence", numProp "Confidence 0.0-1.0." ]
              [ "branchId"; "lens"; "title"; "candidatePlanMarkdown"; "candidatePlanSummary"
                "keyAssumptions"; "keyRisks"; "validationChecks"; "selfCritique"; "confidence" ] }

let parsePlanBranchToolCall (arguments: obj) : Result<PlanBranchCandidate, string> =
    result {
        let! branchId = optString arguments "branchId"
        let! lensRaw = optString arguments "lens"
        let! lens = PlanCommon.parseLens lensRaw
        let! title = optString arguments "title"
        let! candidatePlanMarkdown = optString arguments "candidatePlanMarkdown"
        let! candidatePlanSummary = optString arguments "candidatePlanSummary"
        let! keyAssumptions = optStringArray arguments "keyAssumptions"
        let! keyRisks = optStringArray arguments "keyRisks"
        let! validationChecks = optStringArray arguments "validationChecks"
        let! selfCritique = optString arguments "selfCritique"
        let! confidence = optFloat arguments "confidence"
        return
            { branchId = branchId
              lens = lens
              title = title
              candidatePlanMarkdown = candidatePlanMarkdown
              candidatePlanSummary = candidatePlanSummary
              keyAssumptions = keyAssumptions
              keyRisks = keyRisks
              validationChecks = validationChecks
              selfCritique = selfCritique
              confidence = confidence }
    }

let buildPlanBranchPrompt (req: PlanRequest) (lens: PlanLens) (hyps: PlanHypothesis list) : string =
    let h = hypothesisPacketForBranch hyps (lensName lens)
    let ln = lensName lens
    let ctxText =
        match req.existingContext with
        | Some c when not (System.String.IsNullOrWhiteSpace c) -> "\n\nExisting context:\n" + c
        | _ -> ""
    "You are a planning branch using the \"" + ln + "\" lens. " + lensDescription lens
    + "\n\nYou must NOT write files, run commands, or modify the workspace. You must ONLY call the submit_plan_branch tool."
    + "\nDo not expose the existence of other branches or the judging process in your output."
    + "\nDo not speak as an AI assistant; produce the plan directly."
    + "\n\nRequirement: " + req.normalizedRequirement
    + ctxText
    + "\n\nKey uncertainties for this branch:\n" + h
    + "\n\nCall the submit_plan_branch tool with the branch draft. All required fields must be present and non-empty."
