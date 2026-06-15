module VibeFs.Kernel.PlanEngine

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.PlanTypes

type PlanModelCaller = string -> Async<string>

let private jsonParse (s: string) : obj = JS.JSON.parse(s)

let private tryParseJson (s: string) : obj option =
    try Some(jsonParse s) with | _ -> None

let private extractJsonBlock (raw: string) : string =
    let s = raw.Trim().Replace("```json", "").Replace("```", "").Trim()
    let i = s.IndexOf("{")
    let j = s.LastIndexOf("}")
    if i >= 0 && j > i then s.[i..j] else s

let private toFloat (o: obj) : float =
    if isNull o then 0.0
    else
        match o with
        | :? float as f -> f
        | :? int as i -> float i
        | :? string as s -> (try float s with | _ -> 0.0)
        | _ -> 0.0

let private getString (o: obj) (k: string) : string = str o k
let private getFloat (o: obj) (k: string) : float = toFloat (get o k)

let private getStringArray (o: obj) (k: string) : string list =
    let v = get o k
    if isArray v then (unbox<obj[]> v) |> Array.map string |> Array.toList else []

let lensName (lens: PlanLens) : string =
    match lens with
    | DirectDelivery -> "DirectDelivery" | ArchitectureFirst -> "ArchitectureFirst"
    | RiskFirst -> "RiskFirst" | SimplificationFirst -> "SimplificationFirst"
    | CounterexampleFirst -> "CounterexampleFirst" | CrossDomainFirst -> "CrossDomainFirst"
    | ConstraintFirst -> "ConstraintFirst"

let lensDescription (lens: PlanLens) : string =
    match lens with
    | DirectDelivery -> "Fastest path to an implementable plan."
    | ArchitectureFirst -> "Establish module boundaries and contracts first."
    | RiskFirst -> "Identify failure modes and rollback points first."
    | SimplificationFirst -> "Reduce to the smallest verifiable sub-problem."
    | CounterexampleFirst -> "Hunt for inputs that break the approach."
    | CrossDomainFirst -> "Map the requirement onto other-domain structures."
    | ConstraintFirst -> "Derive the plan from hard constraints."

let normalizeRequirement (raw: string) : string = raw.Trim().Replace("\r\n", "\n")

let formatPlanFileName (hex4: string) : string = "PLAN-" + hex4 + ".md"

let buildPlanLenses (_: PlanRequest) : PlanLens list =
    [ DirectDelivery; ArchitectureFirst; RiskFirst; SimplificationFirst; CrossDomainFirst ]

let buildPlanHypotheses (req: PlanRequest) (_: PlanLens list) : PlanHypothesis list =
    [ { hypothesisId = "h1"; text = "Most ambiguous part of: " + req.normalizedRequirement; targetBranchIds = [] }
      { hypothesisId = "h2"; text = "Which constraints are most likely to conflict?"; targetBranchIds = [] }
      { hypothesisId = "h3"; text = "What hidden assumption, if false, invalidates the most surface area?"; targetBranchIds = [] } ]

let private parseLens (s: string) : PlanLens =
    match s with
    | "ArchitectureFirst" -> ArchitectureFirst | "RiskFirst" -> RiskFirst
    | "SimplificationFirst" -> SimplificationFirst | "CounterexampleFirst" -> CounterexampleFirst
    | "CrossDomainFirst" -> CrossDomainFirst | "ConstraintFirst" -> ConstraintFirst
    | _ -> DirectDelivery

let buildPlanBranchPrompt (req: PlanRequest) (lens: PlanLens) (hyps: PlanHypothesis list) : string =
    let h = hyps |> List.map (fun x -> "- " + x.text) |> String.concat "\n"
    let ln = lensName lens
    "You are a planning branch using the \"" + ln + "\" lens. " + lensDescription lens
    + "\n\nRequirement: " + req.normalizedRequirement
    + "\n\nKey uncertainties:\n" + h
    + "\n\nOutput ONLY valid JSON:\n"
    + "{\"branchId\":\"\",\"lens\":\"" + ln + "\",\"title\":\"\",\"candidatePlanMarkdown\":\"\","
    + "\"candidatePlanSummary\":\"\",\"keyAssumptions\":[],\"keyRisks\":[],\"validationChecks\":[],"
    + "\"selfCritique\":\"\",\"confidence\":0.0}"

let parsePlanBranchResponse (raw: string) : Result<PlanBranchCandidate, string> =
    match tryParseJson (extractJsonBlock raw) with
    | None -> Error "Failed to parse branch JSON"
    | Some o ->
        Ok { branchId = getString o "branchId"; lens = parseLens (getString o "lens")
             title = getString o "title"; candidatePlanMarkdown = getString o "candidatePlanMarkdown"
             candidatePlanSummary = getString o "candidatePlanSummary"; keyAssumptions = getStringArray o "keyAssumptions"
             keyRisks = getStringArray o "keyRisks"; validationChecks = getStringArray o "validationChecks"
             selfCritique = getString o "selfCritique"; confidence = getFloat o "confidence" }

let buildPlanCritiquePrompt (_: PlanRequest) (c: PlanBranchCandidate) : string =
    "Critique plan draft for branch " + c.branchId + " (lens " + lensName c.lens + ")."
    + "\n\nSummary: " + c.candidatePlanSummary + "\nSelf-critique: " + c.selfCritique
    + "\n\nOutput ONLY valid JSON:\n"
    + "{\"branchId\":\"" + c.branchId + "\",\"critiqueMarkdown\":\"\",\"criticalIssues\":[],"
    + "\"missingRequirements\":[],\"counterexamples\":[],\"improvementDirections\":[]}"

let parsePlanCritiqueResponse (raw: string) : Result<PlanBranchCritique, string> =
    match tryParseJson (extractJsonBlock raw) with
    | None -> Error "Failed to parse critique JSON"
    | Some o ->
        Ok { branchId = getString o "branchId"; critiqueMarkdown = getString o "critiqueMarkdown"
             criticalIssues = getStringArray o "criticalIssues"; missingRequirements = getStringArray o "missingRequirements"
             counterexamples = getStringArray o "counterexamples"; improvementDirections = getStringArray o "improvementDirections" }

let buildPlanPoolPrompt (_: PlanRequest) (c: PlanBranchCandidate) (crit: PlanBranchCritique) : string =
    "Generate 3 alternative plan fragments for branch " + c.branchId + "."
    + "\n\nCritique to address: " + crit.critiqueMarkdown
    + "\n\nOutput ONLY a valid JSON array:\n"
    + "[{\"title\":\"\",\"contentMarkdown\":\"\",\"approachSummary\":\"\",\"confidence\":0.0}]"

let parsePlanPoolResponse (raw: string) : Result<PlanPoolEntry list, string> =
    match tryParseJson (extractJsonBlock raw) with
    | None -> Ok []
    | Some o ->
        if isArray o then
            (unbox<obj[]> o) |> Array.mapi (fun i e ->
                { entryId = "e" + string (i + 1); branchId = ""; title = getString e "title"
                  contentMarkdown = getString e "contentMarkdown"; approachSummary = getString e "approachSummary"
                  confidence = getFloat e "confidence" })
            |> Array.toList |> Ok
        else Ok []

let buildPlanRevisionPrompt (_: PlanRequest) (c: PlanBranchCandidate) (crit: PlanBranchCritique) (pool: PlanPoolEntry list) : string =
    let p = pool |> List.map (fun x -> "- " + x.title + ": " + x.contentMarkdown) |> String.concat "\n"
    "Revise the plan for branch " + c.branchId + "."
    + "\n\nOriginal summary: " + c.candidatePlanSummary
    + "\nCritique: " + crit.critiqueMarkdown
    + "\nPool alternatives:\n" + p
    + "\n\nOutput ONLY valid JSON:\n"
    + "{\"branchId\":\"" + c.branchId + "\",\"lens\":\"" + lensName c.lens + "\",\"title\":\"\","
    + "\"revisedPlanMarkdown\":\"\",\"revisedSummary\":\"\",\"keyAssumptions\":[],\"keyRisks\":[],"
    + "\"validationChecks\":[],\"selfCritique\":\"\",\"confidence\":0.0}"

let parsePlanRevisionResponse (raw: string) : Result<PlanBranchRevision, string> =
    match tryParseJson (extractJsonBlock raw) with
    | None -> Error "Failed to parse revision JSON"
    | Some o ->
        Ok { branchId = getString o "branchId"; lens = parseLens (getString o "lens"); title = getString o "title"
             revisedPlanMarkdown = getString o "revisedPlanMarkdown"; revisedPlanSummary = getString o "revisedSummary"
             keyAssumptions = getStringArray o "keyAssumptions"; keyRisks = getStringArray o "keyRisks"
             validationChecks = getStringArray o "validationChecks"; selfCritique = getString o "selfCritique"
             confidence = getFloat o "confidence"
             originalCandidate = Unchecked.defaultof<_>; critique = Unchecked.defaultof<_>; pool = [] }

let buildPlanJudgePrompt (_: PlanRequest) (cands: PlanBranchCandidate list) : string =
    let parts = cands |> List.map (fun c ->
        "Branch " + c.branchId + " (" + lensName c.lens + ", conf " + string c.confidence + "):\n" + c.candidatePlanSummary)
    let lines = String.concat "\n\n" parts
    "Compare these plan candidates and select the winner.\n\n" + lines
    + "\n\nOutput ONLY valid JSON:\n"
    + "{\"winnerBranchId\":\"\",\"keptBranchIds\":[],\"rejectedBranchIds\":[],\"judgeReasoning\":\"\",\"mergeNotes\":[]}"

let parsePlanJudgeResponse (raw: string) : Result<PlanJudgeDecision, string> =
    match tryParseJson (extractJsonBlock raw) with
    | None -> Error "Failed to parse judge JSON"
    | Some o ->
        Ok { winnerBranchId = getString o "winnerBranchId"; keptBranchIds = getStringArray o "keptBranchIds"
             rejectedBranchIds = getStringArray o "rejectedBranchIds"; judgeReasoning = getString o "judgeReasoning"
             mergeNotes = getStringArray o "mergeNotes" }

let renderPlanMarkdown (result: PlanRunResult) : string =
    let hyps = result.hypotheses |> List.map (fun h -> "- " + h.text) |> String.concat "\n"
    let parts = result.revisions |> List.map (fun r ->
        "### " + r.branchId + " — " + r.title + " (" + lensName r.lens + ")\n" + r.revisedPlanSummary)
    let revs = String.concat "\n\n" parts
    let winner = result.revisions |> List.tryFind (fun r -> r.branchId = result.decision.winnerBranchId)
    let plan = match winner with Some r -> r.revisedPlanMarkdown | None -> ""
    let merge = String.concat "\n" result.decision.mergeNotes
    "# " + result.finalFileName + "\n\n## Requirement\n\n" + result.request.normalizedRequirement
    + "\n\n## Uncertainties\n\n" + hyps
    + "\n\n## Branch Overview\n\n" + revs
    + "\n\n## Judge Decision\n\n**Winner:** " + result.decision.winnerBranchId
    + "\n**Rejected:** " + String.concat ", " result.decision.rejectedBranchIds
    + "\n\n" + result.decision.judgeReasoning
    + "\n\n**Merge notes:**\n" + merge
    + "\n\n## Final Plan\n\n" + plan + "\n"

let private emptyCandidate (bid: string) (lens: PlanLens) : PlanBranchCandidate =
    { branchId = bid; lens = lens; title = "Fallback"; candidatePlanMarkdown = ""
      candidatePlanSummary = ""; keyAssumptions = []; keyRisks = []; validationChecks = []
      selfCritique = ""; confidence = 0.0 }

let private emptyCritique (bid: string) : PlanBranchCritique =
    { branchId = bid; critiqueMarkdown = ""; criticalIssues = []; missingRequirements = []
      counterexamples = []; improvementDirections = [] }

let private fallbackRevision (c: PlanBranchCandidate) (cr: PlanBranchCritique) (p: PlanPoolEntry list) : PlanBranchRevision =
    { branchId = c.branchId; lens = c.lens; title = c.title;       revisedPlanMarkdown = c.candidatePlanMarkdown
      revisedPlanSummary = c.candidatePlanSummary; keyAssumptions = c.keyAssumptions; keyRisks = c.keyRisks
      validationChecks = c.validationChecks; selfCritique = c.selfCritique; confidence = c.confidence
      originalCandidate = c; critique = cr; pool = p }

let private revisedToCandidate (r: PlanBranchRevision) : PlanBranchCandidate =
    { branchId = r.branchId
      lens = r.lens
      title = r.title
      candidatePlanMarkdown = r.revisedPlanMarkdown
      candidatePlanSummary = r.revisedPlanSummary
      keyAssumptions = r.keyAssumptions
      keyRisks = r.keyRisks
      validationChecks = r.validationChecks
      selfCritique = r.selfCritique
      confidence = r.confidence }

let runPlanPipeline (request: PlanRequest) (branchCaller: PlanModelCaller) (judgeCaller: PlanModelCaller) : Async<PlanRunResult> =
    async {
        let lenses = buildPlanLenses request
        let hyps = buildPlanHypotheses request lenses
        let indexed = lenses |> List.mapi (fun i l -> ("b" + string (i + 1), l))
        let! drafts = Async.Parallel (indexed |> List.map (fun (bid, lens) ->
            async { let! raw = branchCaller (buildPlanBranchPrompt request lens hyps)
                    return match parsePlanBranchResponse raw with
                           | Ok c -> { c with branchId = bid; lens = lens }
                           | Error _ -> emptyCandidate bid lens }))
        let d = Array.toList drafts
        let! crits = Async.Parallel (d |> List.map (fun c ->
            async { let! raw = branchCaller (buildPlanCritiquePrompt request c)
                    return match parsePlanCritiqueResponse raw with Ok x -> x | Error _ -> emptyCritique c.branchId }))
        let cr = Array.toList crits
        let! pools = Async.Parallel (List.zip d cr |> List.map (fun (c, x) ->
            async { let! raw = branchCaller (buildPlanPoolPrompt request c x)
                    return match parsePlanPoolResponse raw with Ok es -> es |> List.map (fun e -> { e with branchId = c.branchId }) | Error _ -> [] }))
        let pl = Array.toList pools
        let! revisions = Async.Parallel (List.zip3 d cr pl |> List.map (fun (c, x, p) ->
            async { let! raw = branchCaller (buildPlanRevisionPrompt request c x p)
                    return match parsePlanRevisionResponse raw with
                           | Ok r -> { r with originalCandidate = c; critique = x; pool = p; lens = c.lens }
                           | Error _ -> fallbackRevision c x p }))
        let revs = Array.toList revisions
        let! judgeRaw = judgeCaller (buildPlanJudgePrompt request (revs |> List.map revisedToCandidate))
        let decision =
            match parsePlanJudgeResponse judgeRaw with
            | Ok d2 -> d2
            | Error _ -> { winnerBranchId = "b1"; keptBranchIds = ["b1"]; rejectedBranchIds = []
                           judgeReasoning = "Fallback"; mergeNotes = [] }
        let base_ = { request = request; hypotheses = hyps; revisions = revs
                      decision = decision; finalMarkdown = ""; finalFileName = request.outputFileName }
        return { base_ with finalMarkdown = renderPlanMarkdown base_ }
    }
