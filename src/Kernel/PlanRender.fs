module VibeFs.Kernel.PlanRender

open VibeFs.Kernel.PlanTypes
open VibeFs.Kernel.PlanCommon

let renderPlanMarkdown (result: PlanRunResult) : string =
    let bullet items = items |> List.map (fun s -> "- " + s) |> String.concat "\n"
    let numbered items =
        items
        |> List.mapi (fun i s -> string (i + 1) + ". " + s)
        |> String.concat "\n"
    let hyps = bullet (result.hypotheses |> List.map (fun h -> h.text))
    let overview =
        result.revisions
        |> List.map (fun r ->
            sprintf "- **%s** (%s, conf %.2f): %s" r.data.branchId (lensName r.data.lens) r.data.confidence r.data.revisedPlanSummary)
        |> String.concat "\n"
    let comparison =
        result.revisions
        |> List.map (fun r ->
            let status = if result.decision.keptBranchIds |> List.contains r.data.branchId then "kept" else "rejected"
            let risks = if r.data.keyRisks.IsEmpty then "None listed" else String.concat "; " r.data.keyRisks
            sprintf "### %s — %s\n- **Status:** %s\n- **Risks:** %s\n- **Summary:** %s" r.data.branchId r.data.title status risks r.data.revisedPlanSummary)
        |> String.concat "\n\n"
    let winner = result.revisions |> List.tryFind (fun r -> r.data.branchId = result.decision.winnerBranchId)
    let plan, steps, acceptance, risks, openIssues =
        match winner with
        | Some r ->
            r.data.revisedPlanMarkdown,
            numbered r.data.implementationSteps,
            bullet r.data.validationChecks,
            bullet r.data.keyRisks,
            bullet r.data.keyAssumptions
        | None -> "", "", "", "", ""
    let rejectedSummary =
        result.revisions
        |> List.filter (fun r -> result.decision.rejectedBranchIds |> List.contains r.data.branchId)
        |> List.map (fun r -> sprintf "- %s (%s): %s" r.data.branchId (lensName r.data.lens) r.data.revisedPlanSummary)
        |> String.concat "\n"
    let merge = bullet result.decision.mergeNotes
    String.concat "\n\n" [
        "# " + result.finalFileName
        "## 需求\n\n" + result.request.normalizedRequirement
        "## 侦察到的不确定性\n\n" + hyps
        "## 分支总览\n\n" + overview
        "## 分支候选对比\n\n" + comparison
        "## Judge 结论\n\n**Winner:** " + result.decision.winnerBranchId
            + "\n\n" + result.decision.judgeReasoning
            + "\n\n**合并建议：**\n" + merge
        "## 最终计划\n\n" + plan
        "## 实施步骤\n\n" + steps
        "## 验收标准\n\n" + acceptance
        "## 风险与回退\n\n" + risks
        "## 未决问题\n\n" + openIssues
        "## 附录：被淘汰分支摘要\n\n" + (if rejectedSummary = "" then "无" else rejectedSummary)
    ] + "\n"
