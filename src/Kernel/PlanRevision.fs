module VibeFs.Kernel.PlanRevision

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.PlanTypes
open VibeFs.Kernel.PlanCommon

type private ResultBuilder() =
    member _.Bind(m, f) = Result.bind f m
    member _.Return(x) = Ok x
    member _.ReturnFrom(m) = m

let private result = ResultBuilder()

let buildPlanRevisionToolSchema : PlanToolSchema =
    { name = "submit_plan_revision"
      description = "Submit a revised plan branch."
      parameters =
          mkSchema
              [ "branchId", strProp "Branch id."
                "lens", strProp "Lens name."
                "title", strProp "Short title."
                "revisedPlanMarkdown", strProp "Full revised plan markdown."
                "revisedPlanSummary", strProp "One-line summary."
                "keyAssumptions", strArrayProp "Key assumptions."
                "keyRisks", strArrayProp "Key risks."
                "validationChecks", strArrayProp "Validation checks."
                "implementationSteps", strArrayProp "Ordered implementation steps."
                "selfCritique", strProp "Self critique."
                "confidence", numProp "Confidence 0.0-1.0." ]
              [ "branchId"; "lens"; "title"; "revisedPlanMarkdown"; "revisedPlanSummary"
                "keyAssumptions"; "keyRisks"; "validationChecks"; "implementationSteps"; "selfCritique"; "confidence" ] }

let parsePlanRevisionToolCall (arguments: obj) : Result<PlanBranchRevisionData, string> =
    result {
        let! branchId = optString arguments "branchId"
        let! lensRaw = optString arguments "lens"
        let! lens = parseLens lensRaw
        let! title = optString arguments "title"
        let! revisedPlanMarkdown = optString arguments "revisedPlanMarkdown"
        let! revisedPlanSummary = optString arguments "revisedPlanSummary"
        let! keyAssumptions = optStringArray arguments "keyAssumptions"
        let! keyRisks = optStringArray arguments "keyRisks"
        let! validationChecks = optStringArray arguments "validationChecks"
        let! implementationSteps = optStringArray arguments "implementationSteps"
        let! selfCritique = optString arguments "selfCritique"
        let! confidence = optFloat arguments "confidence"
        return
            { branchId = branchId
              lens = lens
              title = title
              revisedPlanMarkdown = revisedPlanMarkdown
              revisedPlanSummary = revisedPlanSummary
              keyAssumptions = keyAssumptions
              keyRisks = keyRisks
              validationChecks = validationChecks
              implementationSteps = implementationSteps
              selfCritique = selfCritique
              confidence = confidence }
    }

let buildPlanRevisionPrompt (_: PlanRequest) (c: PlanBranchCandidate) (crit: PlanBranchCritique) (pool: PlanPoolEntry list) : string =
    let p = pool |> List.map (fun x -> "- " + x.title + ": " + x.contentMarkdown) |> String.concat "\n"
    "Revise the plan for branch " + c.branchId + " based on critique and alternative fragments."
    + "\n\nYou may keep the original direction, but you MUST explicitly address the top critique issues."
    + " If a pool fragment is genuinely better, absorb it and explain why."
    + "\n\nOriginal plan summary: " + c.candidatePlanSummary
    + "\n\nOriginal plan:\n" + c.candidatePlanMarkdown
    + "\n\nCritique:\n" + crit.critiqueMarkdown
    + "\n\nPool alternatives:\n" + p
    + "\n\nCall the submit_plan_revision tool with the revised plan, including ordered implementation steps and validation checks. All required fields must be present and non-empty."
