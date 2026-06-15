module VibeFs.Tests.PlanTests

open Fable.Core.JsInterop
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
    check "buildPlanLenses returns 5" (List.length lenses = 5)
    check "static hypotheses fallback length is 3" (List.length (staticHypothesesForTest req) = 3)

let hypothesesToolCall () =
    let raw = createObj
                  [ "hypotheses",
                    box [| createObj [ "text", box "Ambiguous scope"; "targetBranchIds", box [| "b1"; "b2" |] ] |] ]
    let parsed = parsePlanHypothesesToolCall raw
    check "parsePlanHypothesesToolCall returns 1" (List.length parsed = 1)
    match parsed with
    | h :: _ ->
        equal "hypothesis text" "Ambiguous scope" h.text
        check "targetBranchIds" (h.targetBranchIds = ["b1"; "b2"])
    | [] -> failwith "unexpected empty"

let lensSelection () =
    let req (text: string) =
        { requestId = "r"; rawRequirement = text; normalizedRequirement = text; branchCount = 5
          branchModelName = ""; judgeModelName = ""; outputFileName = "PLAN-x.md"; workspaceRoot = "/"
          existingContext = None }
    let has lens text = buildPlanLenses (req text) |> List.contains lens
    check "constraint heavy picks ConstraintFirst" (has ConstraintFirst "must comply with GDPR")
    check "easy to drift picks CounterexampleFirst" (has CounterexampleFirst "just a quick prototype")
    check "default picks CrossDomainFirst" (has CrossDomainFirst "design a login flow")

let branchToolCall () =
    let raw =
        createObj
            [ "branchId", box "b1"
              "lens", box "ArchitectureFirst"
              "title", box "T"
              "candidatePlanMarkdown", box "# P"
              "candidatePlanSummary", box "S"
              "keyAssumptions", box [| "a" |]
              "keyRisks", box [| "r" |]
              "validationChecks", box [| "v" |]
              "selfCritique", box "c"
              "confidence", box 0.75 ]
    let c = parsePlanBranchToolCall raw
    equal "branchId" "b1" c.branchId
    equal "lens" ArchitectureFirst c.lens
    equal "confidence" 0.75 c.confidence

let judgeToolCall () =
    let raw =
        createObj
            [ "winnerBranchId", box "b2"
              "keptBranchIds", box [| "b2" |]
              "rejectedBranchIds", box [| "b1" |]
              "judgeReasoning", box "r"
              "mergeNotes", box [| "m" |] ]
    let d = parsePlanJudgeToolCall raw
    equal "winner" "b2" d.winnerBranchId
    check "rejected non-empty" (d.rejectedBranchIds <> [])

let revisionToolCall () =
    let raw =
        createObj
            [ "branchId", box "b1"
              "lens", box "ArchitectureFirst"
              "title", box "T"
              "revisedPlanMarkdown", box "# P"
              "revisedPlanSummary", box "S"
              "keyAssumptions", box [| "a" |]
              "keyRisks", box [| "r" |]
              "validationChecks", box [| "v" |]
              "selfCritique", box "c"
              "confidence", box 0.8 ]
    let r = parsePlanRevisionToolCall raw
    equal "revisedPlanSummary parsed" "S" r.revisedPlanSummary

let poolToolCall () =
    let raw =
        createObj
            [ "branchId", box "b1"
              "entries",
              box [| createObj
                         [ "title", box "Alt"
                           "contentMarkdown", box "Body"
                           "approachSummary", box "Approach"
                           "confidence", box 0.6 ] |] ]
    let entries = parsePlanPoolToolCall raw
    check "pool entries length" (List.length entries = 1)
    match entries with
    | e :: _ ->
        equal "entry title" "Alt" e.title
        equal "entry branchId" "b1" e.branchId
    | [] -> failwith "unexpected empty"
