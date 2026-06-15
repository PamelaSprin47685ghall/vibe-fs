module VibeFs.Kernel.PlanPool

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

let private poolItemSchema : obj =
    mkSchema
        [ "title", strProp "Alternative title."
          "contentMarkdown", strProp "Full alternative fragment markdown."
          "approachSummary", strProp "One-line approach summary."
          "confidence", numProp "Confidence 0.0-1.0." ]
        [ "title"; "contentMarkdown"; "approachSummary"; "confidence" ]

let buildPlanPoolToolSchema : PlanToolSchema =
    { name = "submit_plan_pool"
      description = "Submit alternative plan fragments for a branch."
      parameters =
          mkSchema
              [ "branchId", strProp "Branch id."
                "entries", createObj [ "type", box "array"; "items", box poolItemSchema; "description", box "Up to 3 alternative fragments." ] ]
              [ "branchId"; "entries" ] }

let private parseEntry (branchId: string) (index: int) (e: obj) : Result<PlanPoolEntry, string> =
    result {
        let! title = optString e "title"
        let! contentMarkdown = optString e "contentMarkdown"
        let! approachSummary = optString e "approachSummary"
        let! confidence = optFloat e "confidence"
        return
            { entryId = "e" + string (index + 1)
              branchId = branchId
              title = title
              contentMarkdown = contentMarkdown
              approachSummary = approachSummary
              confidence = confidence }
    }

let parsePlanPoolToolCall (arguments: obj) : Result<PlanPoolEntry list, string> =
    result {
        let! branchId = optString arguments "branchId"
        let entries = Dyn.get arguments "entries"
        if not (Dyn.isArray entries) then return! Error "entries: not an array"
        else
            let arr = unbox<obj[]> entries
            let rec traverse (i: int) (acc: PlanPoolEntry list) : Result<PlanPoolEntry list, string> =
                if i >= arr.Length then Ok (List.rev acc)
                else
                    match parseEntry branchId i arr.[i] with
                    | Ok entry -> traverse (i + 1) (entry :: acc)
                    | Error err -> Error err
            return! traverse 0 []
    }

let buildPlanPoolPrompt (_: PlanRequest) (c: PlanBranchCandidate) (crit: PlanBranchCritique) : string =
    "You are expanding the search frontier around a plan branch. Generate up to 3 alternative plan fragments that address the critique or explore genuinely different angles."
    + "\n\nBranch: " + c.branchId + " (lens " + lensName c.lens + ")"
    + "\n\nOriginal plan summary: " + c.candidatePlanSummary
    + "\n\nCritique to address:\n" + crit.critiqueMarkdown
    + "\n\nCall the submit_plan_pool tool with the alternative fragments. All required fields must be present and non-empty."
