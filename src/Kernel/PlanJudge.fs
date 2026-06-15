module VibeFs.Kernel.PlanJudge

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.PlanTypes
open VibeFs.Kernel.PlanCommon

let buildPlanJudgeToolSchema : PlanToolSchema =
    { name = "submit_plan_judge"
      description = "Submit the final plan judge decision."
      parameters =
          mkSchema
              [ "winnerBranchId", strProp "Id of the winning branch."
                "keptBranchIds", strArrayProp "Ids of branches kept as useful."
                "rejectedBranchIds", strArrayProp "Ids of branches rejected."
                "judgeReasoning", strProp "Reasoning for the decision."
                "mergeNotes", strArrayProp "Notes on what to merge into the final plan." ]
              [ "winnerBranchId"; "keptBranchIds"; "rejectedBranchIds"; "judgeReasoning"; "mergeNotes" ] }

let parsePlanJudgeToolCall (arguments: obj) : Result<PlanJudgeDecision, string> =
    match optString arguments "winnerBranchId",
          optStringArray arguments "keptBranchIds",
          optStringArray arguments "rejectedBranchIds",
          optString arguments "judgeReasoning",
          optStringArray arguments "mergeNotes" with
    | Ok winnerBranchId, Ok keptBranchIds, Ok rejectedBranchIds, Ok judgeReasoning, Ok mergeNotes ->
        Ok
            { winnerBranchId = winnerBranchId
              keptBranchIds = keptBranchIds
              rejectedBranchIds = rejectedBranchIds
              judgeReasoning = judgeReasoning
              mergeNotes = mergeNotes }
    | Error e, _, _, _, _ -> Error e
    | _, Error e, _, _, _ -> Error e
    | _, _, Error e, _, _ -> Error e
    | _, _, _, Error e, _ -> Error e
    | _, _, _, _, Error e -> Error e

let buildPlanJudgePrompt (_: PlanRequest) (revs: PlanBranchRevision list) : string =
    let parts =
        revs
        |> List.map (fun r ->
            let poolNotes =
                r.pool
                |> List.map (fun e -> "  - " + e.title + ": " + e.approachSummary)
                |> String.concat "\n"
            let critiqueNotes =
                r.critique.criticalIssues
                |> List.map (fun s -> "  - " + s)
                |> String.concat "\n"
            [ "Branch " + r.data.branchId + " (" + lensName r.data.lens + ", conf " + string r.data.confidence + ")"
              "Summary: " + r.data.revisedPlanSummary
              "Plan:\n" + r.data.revisedPlanMarkdown
              "Key assumptions: " + (String.concat "; " r.data.keyAssumptions)
              "Key risks: " + (String.concat "; " r.data.keyRisks)
              "Validation checks: " + (String.concat "; " r.data.validationChecks)
              "Top critique issues:\n" + critiqueNotes
              "Pool alternatives considered:\n" + poolNotes ]
            |> String.concat "\n")
    let lines = String.concat "\n\n---\n\n" parts
    "You are an independent judge evaluating revised plan candidates.\n"
    + "Pick the single best winner based on: relevance to the requirement, implementability, risk awareness, and structural clarity.\n"
    + "Do NOT reward length, prose style, confidence scores, or abstract language."
    + " Prefer the candidate that best matches the requirement, is most implementable, and has the clearest risk/validation picture.\n\n"
    + lines
    + "\n\nCall the submit_plan_judge tool with your decision. All required fields must be present and non-empty."
