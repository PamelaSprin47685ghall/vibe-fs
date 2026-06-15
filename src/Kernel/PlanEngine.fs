module VibeFs.Kernel.PlanEngine

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.PlanTypes
open VibeFs.Kernel.PlanCommon
open VibeFs.Kernel.PlanHypotheses
open VibeFs.Kernel.PlanBranches
open VibeFs.Kernel.PlanCritique
open VibeFs.Kernel.PlanRevision
open VibeFs.Kernel.PlanRender
open VibeFs.Kernel.PlanJudge
open VibeFs.Kernel.PlanPool


// ---------------------------------------------------------------------------
// Branch draft (delegated to PlanBranches)
// ---------------------------------------------------------------------------

let private emptyCandidate (bid: string) (lens: PlanLens) : PlanBranchCandidate =
    { branchId = bid; lens = lens; title = "Fallback"; candidatePlanMarkdown = ""
      candidatePlanSummary = ""; keyAssumptions = []; keyRisks = []; validationChecks = []
      selfCritique = ""; confidence = 0.0 }

let private emptyCritique (bid: string) : PlanBranchCritique =
    { branchId = bid; critiqueMarkdown = ""; criticalIssues = []; missingRequirements = []
      counterexamples = []; improvementDirections = [] }

let private fallbackRevision (c: PlanBranchCandidate) (cr: PlanBranchCritique) (p: PlanPoolEntry list) : PlanBranchRevision =
    { data =
        { branchId = c.branchId; lens = c.lens; title = c.title; revisedPlanMarkdown = c.candidatePlanMarkdown
          revisedPlanSummary = c.candidatePlanSummary; keyAssumptions = c.keyAssumptions; keyRisks = c.keyRisks
          validationChecks = c.validationChecks; implementationSteps = []
          selfCritique = c.selfCritique; confidence = c.confidence }
      originalCandidate = c; critique = cr; pool = p }

let runPlanPipeline (request: PlanRequest) (branchCaller: PlanModelCaller) (judgeCaller: PlanModelCaller) (hypothesisCaller: PlanModelCaller option) : Async<PlanRunResult> =
    async {
        let lenses = buildPlanLenses request
        let! hypsResult = PlanHypotheses.buildPlanHypotheses request hypothesisCaller lenses
        let hyps =
            match hypsResult with
            | Ok hs -> hs
            | Error _ -> PlanHypotheses.staticHypotheses request
        let indexed = lenses |> List.mapi (fun i l -> ("b" + string (i + 1), l))

        let runBranch (label: string) (work: Async<'a>) (fallback: 'a) : Async<'a> =
            async {
                let! caught = Async.Catch work
                match caught with
                | Choice1Of2 value -> return value
                | Choice2Of2 err ->
                    return fallback
            }

        let draftWorks : Async<PlanBranchCandidate> list =
            indexed
            |> List.map (fun (bid, lens) ->
                runBranch bid
                    (async {
                        let! calls = branchCaller (buildPlanBranchPrompt request lens hyps) [ buildPlanBranchToolSchema ]
                        return
                            calls
                            |> List.tryFind (fun c -> c.toolName = "submit_plan_branch")
                            |> Option.map (fun c ->
                                match parsePlanBranchToolCall c.arguments with
                                | Ok branch -> { branch with branchId = bid; lens = lens }
                                | Error _ -> emptyCandidate bid lens)
                            |> Option.defaultValue (emptyCandidate bid lens)
                    })
                    (emptyCandidate bid lens))

        let! drafts = Async.Parallel draftWorks
        let d = Array.toList drafts

        let critiqueWorks : Async<PlanBranchCritique> list =
            d
            |> List.map (fun c ->
                runBranch c.branchId
                    (async {
                        let! calls = branchCaller (buildPlanCritiquePrompt request c) [ buildPlanCritiqueToolSchema ]
                        return
                            calls
                            |> List.tryFind (fun c -> c.toolName = "submit_plan_critique")
                            |> Option.map (fun c -> parsePlanCritiqueToolCall c.arguments)
                            |> Option.defaultValue (Ok (emptyCritique c.branchId))
                            |> function
                                | Ok critique -> critique
                                | Error _ -> emptyCritique c.branchId
                    })
                    (emptyCritique c.branchId))

        let! crits = Async.Parallel critiqueWorks
        let cr = Array.toList crits

        let fallbackPoolEntries (branchId: string) : PlanPoolEntry list =
            [ { entryId = "e-fallback"; branchId = branchId; title = "No alternatives generated"
                contentMarkdown = "The pool generation step produced no usable alternatives."
                approachSummary = "Continue with the original plan."
                confidence = 0.0 } ]

        let poolWorks : Async<PlanPoolEntry list> list =
            List.zip d cr
            |> List.map (fun (c, x) ->
                runBranch c.branchId
                    (async {
                        let! calls = branchCaller (buildPlanPoolPrompt request c x) [ buildPlanPoolToolSchema ]
                        return
                            calls
                            |> List.tryFind (fun c -> c.toolName = "submit_plan_pool")
                            |> Option.map (fun toolCall ->
                                match parsePlanPoolToolCall toolCall.arguments with
                                | Ok entries -> entries
                                | Error _ -> fallbackPoolEntries c.branchId)
                            |> Option.defaultValue (fallbackPoolEntries c.branchId)
                    })
                    (fallbackPoolEntries c.branchId))

        let! pools = Async.Parallel poolWorks
        let pl = Array.toList pools

        let revisionWorks : Async<PlanBranchRevision> list =
            List.zip3 d cr pl
            |> List.map (fun (c, x, p) ->
                runBranch c.branchId
                    (async {
                        let! calls = branchCaller (buildPlanRevisionPrompt request c x p) [ buildPlanRevisionToolSchema ]
                        return
                            calls
                            |> List.tryFind (fun c -> c.toolName = "submit_plan_revision")
                            |> Option.bind (fun toolCall ->
                                match parsePlanRevisionToolCall toolCall.arguments with
                                | Ok data ->
                                    let d = { data with branchId = c.branchId; lens = c.lens }
                                    Some { data = d; originalCandidate = c; critique = x; pool = p }
                                | Error _ -> None)
                            |> Option.defaultValue (fallbackRevision c x p)
                    })
                    (fallbackRevision c x p))

        let! revisions = Async.Parallel revisionWorks
        let revs = Array.toList revisions

        let! judgeCalls = judgeCaller (buildPlanJudgePrompt request revs) [ buildPlanJudgeToolSchema ]
        let fallbackDecision =
            let winner =
                revs
                |> List.sortByDescending (fun r -> r.data.confidence)
                |> List.tryHead
            match winner with
            | Some r ->
                { winnerBranchId = r.data.branchId
                  keptBranchIds = [ r.data.branchId ]
                  rejectedBranchIds = revs |> List.map (fun x -> x.data.branchId) |> List.filter (fun id -> id <> r.data.branchId)
                  judgeReasoning = "Judge did not return a decision; selected the highest-confidence branch as fallback."
                  mergeNotes = [] }
            | None ->
                { winnerBranchId = "b1"; keptBranchIds = ["b1"]; rejectedBranchIds = []
                  judgeReasoning = "Judge did not return a decision and no branches were available."
                  mergeNotes = [] }
        let decision =
            judgeCalls
            |> List.tryFind (fun c -> c.toolName = "submit_plan_judge")
            |> Option.map (fun c -> parsePlanJudgeToolCall c.arguments)
            |> Option.defaultValue (Ok fallbackDecision)
            |> Result.defaultValue fallbackDecision

        let base_ =
            { request = request; hypotheses = hyps; revisions = revs
              decision = decision; finalMarkdown = ""; finalFileName = request.outputFileName }
        return { base_ with finalMarkdown = renderPlanMarkdown base_ }
    }
