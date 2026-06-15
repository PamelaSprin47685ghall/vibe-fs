module VibeFs.Kernel.PlanEngine

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.PlanTypes

type PlanModelCaller = VibeFs.Kernel.PlanTypes.PlanModelCaller

let private optString (o: obj) (k: string) : string =
    let v = Dyn.get o k
    if Dyn.isNullish v then "" else string v

let private optStringArray (o: obj) (k: string) : string list =
    let v = Dyn.get o k
    if Dyn.isArray v then (unbox<obj[]> v) |> Array.map string |> Array.toList else []

let private optFloat (o: obj) (k: string) : float =
    let v = Dyn.get o k
    if Dyn.isNullish v then 0.0
    else
        match v with
        | :? float as f -> f
        | :? int as i -> float i
        | :? string as s -> (try float s with | _ -> 0.0)
        | _ -> 0.0

let private mkSchema (props: (string * obj) list) (required: string list) : obj =
    createObj
        [ "type", box "object"
          "properties", box (createObj [ for (k, v) in props -> k, v ])
          "required", box (Array.ofList required)
          "additionalProperties", box false ]

let private strProp (desc: string) : obj = createObj [ "type", box "string"; "description", box desc ]
let private strArrayProp (desc: string) : obj =
    createObj [ "type", box "array"; "items", box (createObj [ "type", box "string" ]); "description", box desc ]
let private numProp (desc: string) : obj = createObj [ "type", box "number"; "description", box desc ]

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

let private looksConstraintHeavy (req: string) : bool =
    let terms = ["must"; "must not"; "required"; "constraint"; "compliance"; "regulation"; "gdpr"; "security"; "audit"; "permission"; "license"; "legal"; "performance budget"; "sla"; "latency"; "throughput"; "quota"; "limit"]
    let lower = req.ToLowerInvariant()
    terms |> List.exists (fun t -> lower.Contains(t))

let private looksEasyToDrift (req: string) : bool =
    let terms = ["minimal"; "simple"; "just"; "quick"; "easy"; "small"; "tiny"; "mvp"; "prototype"; "hack"; "workaround"; "temporary"; "for now"]
    let lower = req.ToLowerInvariant()
    terms |> List.exists (fun t -> lower.Contains(t))

let buildPlanLenses (req: PlanRequest) : PlanLens list =
    [ DirectDelivery; ArchitectureFirst; RiskFirst; SimplificationFirst
      (if looksConstraintHeavy req.normalizedRequirement then ConstraintFirst
       elif looksEasyToDrift req.normalizedRequirement then CounterexampleFirst
       else CrossDomainFirst) ]

// ---------------------------------------------------------------------------
// Hypotheses
// ---------------------------------------------------------------------------

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

let parsePlanHypothesesToolCall (arguments: obj) : PlanHypothesis list =
    let hyps = Dyn.get arguments "hypotheses"
    if Dyn.isArray hyps then
        (unbox<obj[]> hyps)
        |> Array.mapi (fun i e ->
            { hypothesisId = "h" + string (i + 1)
              text = optString e "text"
              targetBranchIds = optStringArray e "targetBranchIds" })
        |> Array.toList
    else []

let buildPlanHypothesesPrompt (req: PlanRequest) : string =
    "You are a requirement scout. Given the following requirement, identify the 3 most important uncertainties that could invalidate or reshape any plan.\n\n"
    + "Requirement:\n" + req.normalizedRequirement
    + "\n\nCall the submit_plan_hypotheses tool with your answer."

let private staticHypotheses (req: PlanRequest) : PlanHypothesis list =
    [ { hypothesisId = "h1"; text = "Most ambiguous part of: " + req.normalizedRequirement; targetBranchIds = [] }
      { hypothesisId = "h2"; text = "Which constraints are most likely to conflict?"; targetBranchIds = [] }
      { hypothesisId = "h3"; text = "What hidden assumption, if false, invalidates the most surface area?"; targetBranchIds = [] } ]

let staticHypothesesForTest (req: PlanRequest) : PlanHypothesis list = staticHypotheses req

let buildPlanHypotheses (req: PlanRequest) (caller: PlanModelCaller option) (lenses: PlanLens list) : Async<PlanHypothesis list> =
    match caller with
    | None -> async { return staticHypotheses req }
    | Some call ->
        async {
            let! calls = call (buildPlanHypothesesPrompt req) [ buildPlanHypothesesToolSchema ]
            return
                calls
                |> List.tryFind (fun c -> c.toolName = "submit_plan_hypotheses")
                |> Option.map (fun c -> parsePlanHypothesesToolCall c.arguments)
                |> Option.defaultValue (staticHypotheses req)
        }

let private hypothesisPacketForBranch (hyps: PlanHypothesis list) (branchId: string) : string =
    let relevant =
        hyps
        |> List.filter (fun h -> List.isEmpty h.targetBranchIds || List.contains branchId h.targetBranchIds)
    if List.isEmpty relevant then "No specific uncertainties flagged for this branch."
    else
        relevant
        |> List.map (fun h -> "- " + h.text)
        |> String.concat "\n"

// ---------------------------------------------------------------------------
// Branch draft
// ---------------------------------------------------------------------------

let private parseLens (s: string) : PlanLens =
    match s with
    | "ArchitectureFirst" -> ArchitectureFirst | "RiskFirst" -> RiskFirst
    | "SimplificationFirst" -> SimplificationFirst | "CounterexampleFirst" -> CounterexampleFirst
    | "CrossDomainFirst" -> CrossDomainFirst | "ConstraintFirst" -> ConstraintFirst
    | _ -> DirectDelivery

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

let parsePlanBranchToolCall (arguments: obj) : PlanBranchCandidate =
    { branchId = optString arguments "branchId"
      lens = parseLens (optString arguments "lens")
      title = optString arguments "title"
      candidatePlanMarkdown = optString arguments "candidatePlanMarkdown"
      candidatePlanSummary = optString arguments "candidatePlanSummary"
      keyAssumptions = optStringArray arguments "keyAssumptions"
      keyRisks = optStringArray arguments "keyRisks"
      validationChecks = optStringArray arguments "validationChecks"
      selfCritique = optString arguments "selfCritique"
      confidence = optFloat arguments "confidence" }

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
    + "\n\nCall the submit_plan_branch tool with the branch draft."

// ---------------------------------------------------------------------------
// Critique
// ---------------------------------------------------------------------------

let buildPlanCritiqueToolSchema : PlanToolSchema =
    { name = "submit_plan_critique"
      description = "Submit a critique of a plan branch."
      parameters =
          mkSchema
              [ "branchId", strProp "Branch id."
                "critiqueMarkdown", strProp "Full critique markdown."
                "criticalIssues", strArrayProp "Critical issues."
                "missingRequirements", strArrayProp "Missing requirements."
                "counterexamples", strArrayProp "Counterexamples."
                "improvementDirections", strArrayProp "Improvement directions." ]
              [ "branchId"; "critiqueMarkdown"; "criticalIssues"; "missingRequirements"; "counterexamples"; "improvementDirections" ] }

let parsePlanCritiqueToolCall (arguments: obj) : PlanBranchCritique =
    { branchId = optString arguments "branchId"
      critiqueMarkdown = optString arguments "critiqueMarkdown"
      criticalIssues = optStringArray arguments "criticalIssues"
      missingRequirements = optStringArray arguments "missingRequirements"
      counterexamples = optStringArray arguments "counterexamples"
      improvementDirections = optStringArray arguments "improvementDirections" }

let buildPlanCritiquePrompt (_: PlanRequest) (c: PlanBranchCandidate) : string =
    "You are a ruthless critic reviewing a plan draft. Identify concrete problems, missing requirements, unstated assumptions, and ways the plan could fail. Do NOT propose fixes; only diagnose."
    + "\n\nBranch: " + c.branchId + " (lens " + lensName c.lens + ")"
    + "\n\nPlan:\n" + c.candidatePlanMarkdown
    + "\n\nSummary: " + c.candidatePlanSummary
    + "\nSelf-critique: " + c.selfCritique
    + "\n\nCall the submit_plan_critique tool with your diagnosis."

// ---------------------------------------------------------------------------
// Pool
// ---------------------------------------------------------------------------

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

let parsePlanPoolToolCall (arguments: obj) : PlanPoolEntry list =
    let branchId = optString arguments "branchId"
    let entries = Dyn.get arguments "entries"
    if Dyn.isArray entries then
        (unbox<obj[]> entries)
        |> Array.mapi (fun i e ->
            { entryId = "e" + string (i + 1); branchId = branchId
              title = optString e "title"; contentMarkdown = optString e "contentMarkdown"
              approachSummary = optString e "approachSummary"; confidence = optFloat e "confidence" })
        |> Array.toList
    else []

let buildPlanPoolPrompt (_: PlanRequest) (c: PlanBranchCandidate) (crit: PlanBranchCritique) : string =
    "You are expanding the search frontier around a plan branch. Generate up to 3 alternative plan fragments that address the critique or explore genuinely different angles."
    + "\n\nBranch: " + c.branchId + " (lens " + lensName c.lens + ")"
    + "\n\nOriginal plan summary: " + c.candidatePlanSummary
    + "\n\nCritique to address:\n" + crit.critiqueMarkdown
    + "\n\nCall the submit_plan_pool tool with the alternative fragments."

// ---------------------------------------------------------------------------
// Revision
// ---------------------------------------------------------------------------

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
                "selfCritique", strProp "Self critique."
                "confidence", numProp "Confidence 0.0-1.0." ]
              [ "branchId"; "lens"; "title"; "revisedPlanMarkdown"; "revisedPlanSummary"
                "keyAssumptions"; "keyRisks"; "validationChecks"; "selfCritique"; "confidence" ] }

let parsePlanRevisionToolCall (arguments: obj) : PlanBranchRevision =
    { branchId = optString arguments "branchId"
      lens = parseLens (optString arguments "lens")
      title = optString arguments "title"
      revisedPlanMarkdown = optString arguments "revisedPlanMarkdown"
      revisedPlanSummary = optString arguments "revisedPlanSummary"
      keyAssumptions = optStringArray arguments "keyAssumptions"
      keyRisks = optStringArray arguments "keyRisks"
      validationChecks = optStringArray arguments "validationChecks"
      selfCritique = optString arguments "selfCritique"
      confidence = optFloat arguments "confidence"
      originalCandidate = Unchecked.defaultof<_>; critique = Unchecked.defaultof<_>; pool = [] }

let buildPlanRevisionPrompt (_: PlanRequest) (c: PlanBranchCandidate) (crit: PlanBranchCritique) (pool: PlanPoolEntry list) : string =
    let p = pool |> List.map (fun x -> "- " + x.title + ": " + x.contentMarkdown) |> String.concat "\n"
    "Revise the plan for branch " + c.branchId + " based on critique and alternative fragments."
    + "\n\nYou may keep the original direction, but you MUST explicitly address the top critique issues."
    + " If a pool fragment is genuinely better, absorb it and explain why."
    + "\n\nOriginal plan summary: " + c.candidatePlanSummary
    + "\n\nOriginal plan:\n" + c.candidatePlanMarkdown
    + "\n\nCritique:\n" + crit.critiqueMarkdown
    + "\n\nPool alternatives:\n" + p
    + "\n\nCall the submit_plan_revision tool with the revised plan."

// ---------------------------------------------------------------------------
// Judge
// ---------------------------------------------------------------------------

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

let parsePlanJudgeToolCall (arguments: obj) : PlanJudgeDecision =
    { winnerBranchId = optString arguments "winnerBranchId"
      keptBranchIds = optStringArray arguments "keptBranchIds"
      rejectedBranchIds = optStringArray arguments "rejectedBranchIds"
      judgeReasoning = optString arguments "judgeReasoning"
      mergeNotes = optStringArray arguments "mergeNotes" }

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
            [ "Branch " + r.branchId + " (" + lensName r.lens + ", conf " + string r.confidence + ")"
              "Summary: " + r.revisedPlanSummary
              "Plan:\n" + r.revisedPlanMarkdown
              "Key assumptions: " + (String.concat "; " r.keyAssumptions)
              "Key risks: " + (String.concat "; " r.keyRisks)
              "Validation checks: " + (String.concat "; " r.validationChecks)
              "Top critique issues:\n" + critiqueNotes
              "Pool alternatives considered:\n" + poolNotes ]
            |> String.concat "\n")
    let lines = String.concat "\n\n---\n\n" parts
    "You are an independent judge evaluating revised plan candidates.\n"
    + "Pick the single best winner based on: relevance to the requirement, implementability, risk awareness, and structural clarity.\n"
    + "Do NOT reward length, prose style, confidence scores, or abstract language."
    + " Prefer the candidate that best matches the requirement, is most implementable, and has the clearest risk/validation picture.\n\n"
    + lines
    + "\n\nCall the submit_plan_judge tool with your decision."

// ---------------------------------------------------------------------------
// Rendering
// ---------------------------------------------------------------------------

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
            sprintf "- **%s** (%s, conf %.2f): %s" r.branchId (lensName r.lens) r.confidence r.revisedPlanSummary)
        |> String.concat "\n"
    let comparison =
        result.revisions
        |> List.map (fun r ->
            let status = if result.decision.keptBranchIds |> List.contains r.branchId then "kept" else "rejected"
            let risks = if r.keyRisks.IsEmpty then "None listed" else String.concat "; " r.keyRisks
            sprintf "### %s — %s\n- **Status:** %s\n- **Risks:** %s\n- **Summary:** %s" r.branchId r.title status risks r.revisedPlanSummary)
        |> String.concat "\n\n"
    let winner = result.revisions |> List.tryFind (fun r -> r.branchId = result.decision.winnerBranchId)
    let plan, steps, acceptance, risks, openIssues =
        match winner with
        | Some r ->
            r.revisedPlanMarkdown,
            numbered r.validationChecks,
            bullet r.validationChecks,
            bullet r.keyRisks,
            bullet r.keyAssumptions
        | None -> "", "", "", "", ""
    let rejectedSummary =
        result.revisions
        |> List.filter (fun r -> result.decision.rejectedBranchIds |> List.contains r.branchId)
        |> List.map (fun r -> sprintf "- %s (%s): %s" r.branchId (lensName r.lens) r.revisedPlanSummary)
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

let private emptyCandidate (bid: string) (lens: PlanLens) : PlanBranchCandidate =
    { branchId = bid; lens = lens; title = "Fallback"; candidatePlanMarkdown = ""
      candidatePlanSummary = ""; keyAssumptions = []; keyRisks = []; validationChecks = []
      selfCritique = ""; confidence = 0.0 }

let private emptyCritique (bid: string) : PlanBranchCritique =
    { branchId = bid; critiqueMarkdown = ""; criticalIssues = []; missingRequirements = []
      counterexamples = []; improvementDirections = [] }

let private fallbackRevision (c: PlanBranchCandidate) (cr: PlanBranchCritique) (p: PlanPoolEntry list) : PlanBranchRevision =
    { branchId = c.branchId; lens = c.lens; title = c.title; revisedPlanMarkdown = c.candidatePlanMarkdown
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

let runPlanPipeline (request: PlanRequest) (branchCaller: PlanModelCaller) (judgeCaller: PlanModelCaller) (hypothesisCaller: PlanModelCaller option) : Async<PlanRunResult> =
    async {
        let lenses = buildPlanLenses request
        let! hyps = buildPlanHypotheses request hypothesisCaller lenses
        let indexed = lenses |> List.mapi (fun i l -> ("b" + string (i + 1), l))

        let! drafts =
            Async.Parallel
                (indexed
                 |> List.map (fun (bid, lens) ->
                     async {
                         let! calls = branchCaller (buildPlanBranchPrompt request lens hyps) [ buildPlanBranchToolSchema ]
                         return
                             calls
                             |> List.tryFind (fun c -> c.toolName = "submit_plan_branch")
                             |> Option.map (fun c -> { parsePlanBranchToolCall c.arguments with branchId = bid; lens = lens })
                             |> Option.defaultValue (emptyCandidate bid lens)
                     }))
        let d = Array.toList drafts

        let! crits =
            Async.Parallel
                (d
                 |> List.map (fun c ->
                     async {
                         let! calls = branchCaller (buildPlanCritiquePrompt request c) [ buildPlanCritiqueToolSchema ]
                         return
                             calls
                             |> List.tryFind (fun c -> c.toolName = "submit_plan_critique")
                             |> Option.map (fun c -> parsePlanCritiqueToolCall c.arguments)
                             |> Option.defaultValue (emptyCritique c.branchId)
                     }))
        let cr = Array.toList crits

        let! pools =
            Async.Parallel
                (List.zip d cr
                 |> List.map (fun (c, x) ->
                     async {
                         let! calls = branchCaller (buildPlanPoolPrompt request c x) [ buildPlanPoolToolSchema ]
                         return
                             calls
                             |> List.tryFind (fun c -> c.toolName = "submit_plan_pool")
                             |> Option.map (fun c -> parsePlanPoolToolCall c.arguments)
                             |> Option.defaultValue
                                 [ { entryId = "e-fallback"; branchId = c.branchId; title = "No alternatives generated"
                                     contentMarkdown = "The pool generation step produced no usable alternatives."
                                     approachSummary = "Continue with the original plan."
                                     confidence = 0.0 } ]
                     }))
        let pl = Array.toList pools

        let! revisions =
            Async.Parallel
                (List.zip3 d cr pl
                 |> List.map (fun (c, x, p) ->
                     async {
                         let! calls = branchCaller (buildPlanRevisionPrompt request c x p) [ buildPlanRevisionToolSchema ]
                         return
                             calls
                             |> List.tryFind (fun c -> c.toolName = "submit_plan_revision")
                              |> Option.map (fun toolCall ->
                                  let parsed = parsePlanRevisionToolCall toolCall.arguments
                                  { parsed with
                                      branchId = c.branchId
                                      lens = c.lens
                                      originalCandidate = c
                                      critique = x
                                      pool = p })
                             |> Option.defaultValue (fallbackRevision c x p)
                     }))
        let revs = Array.toList revisions

        let! judgeCalls = judgeCaller (buildPlanJudgePrompt request revs) [ buildPlanJudgeToolSchema ]
        let decision =
            judgeCalls
            |> List.tryFind (fun c -> c.toolName = "submit_plan_judge")
            |> Option.map (fun c -> parsePlanJudgeToolCall c.arguments)
            |> Option.defaultValue
                { winnerBranchId = "b1"; keptBranchIds = ["b1"]; rejectedBranchIds = []
                  judgeReasoning = "Fallback"; mergeNotes = [] }

        let base_ =
            { request = request; hypotheses = hyps; revisions = revs
              decision = decision; finalMarkdown = ""; finalFileName = request.outputFileName }
        return { base_ with finalMarkdown = renderPlanMarkdown base_ }
    }
