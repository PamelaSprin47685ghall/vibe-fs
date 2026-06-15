module VibeFs.Tests.PlanTests

open VibeFs.Kernel
open VibeFs.Kernel.PlanTypes
open VibeFs.Kernel.PlanEngine
open VibeFs.Tests.Assert

let normalize () =
    equal "normalize trims and normalizes line endings" "a\nb" (normalizeRequirement "a\r\nb  ")

let fileName () =
    equal "formatPlanFileName" "PLAN-7f3a.md" (formatPlanFileName "7f3a")

let lenses () =
    let req =
        { requestId = "r"; rawRequirement = "x"; normalizedRequirement = "x"; branchCount = 3
          branchModelName = ""; judgeModelName = ""; outputFileName = "PLAN-x.md"; workspaceRoot = "/"
          existingContext = None }
    check "buildPlanLenses returns 5 lenses" (List.length (buildPlanLenses req) = 5)

let hypotheses () =
    let req =
        { requestId = "r"; rawRequirement = "x"; normalizedRequirement = "x"; branchCount = 3
          branchModelName = ""; judgeModelName = ""; outputFileName = "PLAN-x.md"; workspaceRoot = "/"
          existingContext = None }
    let lenses = buildPlanLenses req
    check "buildPlanHypotheses returns 3" (List.length (buildPlanHypotheses req lenses) = 3)

let branchParse () =
    let raw =
        "{\"branchId\":\"b1\",\"lens\":\"ArchitectureFirst\",\"title\":\"T\","
        + "\"candidatePlanMarkdown\":\"# P\",\"candidatePlanSummary\":\"S\","
        + "\"keyAssumptions\":[\"a\"],\"keyRisks\":[\"r\"],\"validationChecks\":[\"v\"],"
        + "\"selfCritique\":\"c\",\"confidence\":0.75}"
    match parsePlanBranchResponse raw with
    | Ok c ->
        equal "branchId" "b1" c.branchId
        equal "lens" ArchitectureFirst c.lens
        equal "confidence" 0.75 c.confidence
    | Error e -> failwith e

let judgeParse () =
    let raw =
        "{\"winnerBranchId\":\"b2\",\"keptBranchIds\":[\"b2\"],\"rejectedBranchIds\":[\"b1\"],"
        + "\"judgeReasoning\":\"r\",\"mergeNotes\":[\"m\"]}"
    match parsePlanJudgeResponse raw with
    | Ok d ->
        equal "winner" "b2" d.winnerBranchId
        check "rejected non-empty" (d.rejectedBranchIds <> [])
    | Error e -> failwith e
