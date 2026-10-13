namespace Wanxiangshu.Mission.Work.Review

open Fable.Core.JsInterop
open Wanxiangshu.Mission.Work

module Surface =
    let schemaJson = Model.schemaJson

    let private findingObject (finding: ReviewFinding) =
        box
            {| acceptance_criteria = finding.AcceptanceCriteria
               work_plan = finding.WorkPlan |}

    let parse (value: obj) =
        match Model.tryParse value with
        | Error error -> box {| ok = false; error = error |}
        | Ok findings ->
            let values = ReviewFindings.values findings

            box
                {| ok = true
                   findings = values |> List.map findingObject |> List.toArray
                   passed = ReviewFindings.isEmpty findings |}
