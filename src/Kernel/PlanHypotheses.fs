module VibeFs.Kernel.PlanHypotheses

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.PlanTypes
open VibeFs.Kernel.PlanCommon

let private hypothesisItemSchema : obj =
    mkSchema
        [ "text", strProp "The uncertainty as a clear statement."
          "targetBranchIds", strArrayProp "Branch ids this uncertainty is most relevant to, or empty if global." ]
        [ "text" ]

let buildPlanHypothesesToolSchema : PlanToolSchema =
    { name = "submit_plan_hypotheses"
      description = "Submit the identified requirement uncertainties."
      parameters =
          mkSchema
              [ "hypotheses",
                createObj
                    [ "type", box "array"
                      "items", box hypothesisItemSchema
                      "description", box "Array of uncertainty objects." ] ]
              [ "hypotheses" ] }

let parsePlanHypothesesToolCall (arguments: obj) : Result<PlanHypothesis list, string> =
    if Dyn.isNullish arguments then Error "arguments object is null"
    else
        let hyps = Dyn.get arguments "hypotheses"
        if not (Dyn.isArray hyps) then Error "hypotheses field is missing or not an array"
        else
            let arr = unbox<obj[]> hyps
            let rec traverse (i: int) (acc: PlanHypothesis list) : Result<PlanHypothesis list, string> =
                if i >= arr.Length then Ok (List.rev acc)
                else
                    let e = arr.[i]
                    match optString e "text" with
                    | Error msg -> Error $"hypothesis[{i}]: {msg}"
                    | Ok text ->
                        let targetBranchIds =
                            match optStringArray e "targetBranchIds" with
                            | Ok xs -> xs
                            | Error _ -> []
                        traverse (i + 1) ({ hypothesisId = "h" + string (i + 1); text = text; targetBranchIds = targetBranchIds } :: acc)
            traverse 0 []

let buildPlanHypothesesPrompt (req: PlanRequest) : string =
    "You are a requirement scout. Given the following requirement, identify the 3 most important uncertainties that could invalidate or reshape any plan.\n\n"
    + "Requirement:\n" + req.normalizedRequirement
    + "\n\nCall the submit_plan_hypotheses tool with your answer."

let staticHypotheses (req: PlanRequest) : PlanHypothesis list =
    [ { hypothesisId = "h1"; text = "Most ambiguous part of: " + req.normalizedRequirement; targetBranchIds = [] }
      { hypothesisId = "h2"; text = "Which constraints are most likely to conflict?"; targetBranchIds = [] }
      { hypothesisId = "h3"; text = "What hidden assumption, if false, invalidates the most surface area?"; targetBranchIds = [] } ]

let buildPlanHypotheses (req: PlanRequest) (caller: PlanModelCaller option) (lenses: PlanLens list) : Async<Result<PlanHypothesis list, string>> =
    ignore lenses
    match caller with
    | None -> async { return Error "No hypothesis model caller provided" }
    | Some call ->
        async {
            let! calls = call (buildPlanHypothesesPrompt req) [ buildPlanHypothesesToolSchema ]
            let validToolCalls = calls |> List.filter (fun c -> c.toolName = "submit_plan_hypotheses")
            if List.isEmpty validToolCalls then
                return Error "No submit_plan_hypotheses tool call returned"
            else
                return
                    validToolCalls
                    |> List.tryHead
                    |> Option.map (fun c -> parsePlanHypothesesToolCall c.arguments)
                    |> Option.defaultValue (Error "No valid hypothesis submission")
        }

let hypothesisPacketForBranch (hyps: PlanHypothesis list) (branchId: string) : string =
    let relevant =
        hyps
        |> List.filter (fun h -> List.isEmpty h.targetBranchIds || List.contains branchId h.targetBranchIds)
    if List.isEmpty relevant then "No specific uncertainties flagged for this branch."
    else
        relevant
        |> List.map (fun h -> "- " + h.text)
        |> String.concat "\n"
