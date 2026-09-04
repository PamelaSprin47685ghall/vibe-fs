// WHAT[EPI-010,EPI-016,EPI-024]: Gec composition over certificate slots, exact/astar/mcts refiners and ordinal inference.
namespace Wanxiangshu.Sphinx

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Sphinx.Core
open Wanxiangshu.Sphinx.Runtime

module GecRefine =

    let private isFiniteNumber (value: float) : bool =
        not (Double.IsNaN value || Double.IsInfinity value)

    module BayesExact = Wanxiangshu.Sphinx.Plugins.Bayes.Exact
    module AStarRefiner = Wanxiangshu.Sphinx.Plugins.AStar.Refiner
    module MctsRefiner = Wanxiangshu.Sphinx.Plugins.Mcts.Refiner
    module OrdinalInference = Wanxiangshu.Sphinx.Plugins.Ordinal.Inference

    let private isUndefined (value: obj) : bool = emitJsExpr value "$0 === undefined"

    let private isNullish (value: obj) : bool = isNull value || isUndefined value

    let private isJsArray (value: obj) : bool = emitJsExpr value "Array.isArray($0)"

    let private fieldOf (value: obj) (name: string) : obj =
        if isNullish value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

    let private textOf (value: obj) : string =
        if isNullish value then "" else string value

    let private arrayOf (value: obj) : obj array =
        if isNullish value || not (isJsArray value) then
            [||]
        else
            unbox<obj array> value

    let private stringArrayOf (value: obj) : string list =
        arrayOf value |> Array.map textOf |> Array.toList

    let private keysOf (value: obj) : string array = emitJsExpr value "Object.keys($0)"

    let private floatField (value: obj) (name: string) : float option =
        let entry = fieldOf value name

        if isNullish entry then
            None
        else
            let number: float = emitJsExpr entry "$0"

            if isFiniteNumber number then Some number else None

    let private numberOf (value: obj) : float option =
        if isNullish value then
            None
        else
            let number: float = emitJsExpr value "$0"

            if isFiniteNumber number then Some number else None

    let private floatMapKeep (value: obj) : Map<string, float> =
        if isNullish value then
            Map.empty
        else
            keysOf value
            |> Array.toList
            |> List.map (fun key ->
                let number: float = emitJsExpr (value, key) "$0[$1]"
                key, number)
            |> Map.ofList

    let private stringListMapOf (value: obj) : Map<string, string list> =
        if isNullish value then
            Map.empty
        else
            keysOf value
            |> Array.toList
            |> List.map (fun key -> key, stringArrayOf (fieldOf value key))
            |> Map.ofList

    let private typedError (code: string) (message: string) : obj =
        box
            {| ok = false
               error = box {| code = code; message = message |} |}

    let private stringError (message: string) : obj = box {| ok = false; error = message |}

    let private okResult (fields: (string * obj) list) : obj = ("ok", box true) :: fields |> createObj

    let private mapView (entries: Map<string, float>) : obj =
        entries
        |> Map.toList
        |> List.map (fun (key, value) -> key ==> value)
        |> createObj

    [<RequireQualifiedAccess>]
    type private CertFault =
        | InvalidNode of detail: string
        | Upstream of code: string * message: string

    let private certFaultCode (fault: CertFault) : string =
        match fault with
        | CertFault.InvalidNode _ -> "invalid-certificate"
        | CertFault.Upstream(code, _) -> code

    let private certFaultMessage (fault: CertFault) : string =
        match fault with
        | CertFault.InvalidNode detail -> sprintf "certificate node id is unusable: %s" detail
        | CertFault.Upstream(_, message) -> message

    type private DecodedCertificate =
        { NodeId: NodeId
          Siblings: CertificatePatchRequest list
          Witnesses: EventId list
          Derivations: EventId list
          Revision: int64 }

    let private decodeEventIds (certificate: obj) (name: string) : EventId list =
        arrayOf (fieldOf certificate name)
        |> Array.toList
        |> List.choose (fun entry ->
            match EventId.tryCreate (textOf entry) with
            | Ok event -> Some event
            | Error _ -> None)

    let private guaranteeScopeOf (entry: obj) (slot: string) : string =
        let raw = textOf (fieldOf entry "scope")

        if String.IsNullOrWhiteSpace raw then slot else raw

    let private decodeGuaranteeKind (entry: obj) (slot: string) : CertificateGuarantee =
        let kind = textOf (fieldOf entry "kind")

        let assumptions =
            stringArrayOf (fieldOf entry "assumptions")
            |> List.filter (not << String.IsNullOrWhiteSpace)
            |> Set.ofList

        match kind with
        | "coverage" ->
            let level = floatField entry "level" |> Option.defaultValue 0.95
            let margin = floatField entry "error" |> Option.defaultValue 0.0

            ProbabilisticCoverage(level, margin, assumptions, guaranteeScopeOf entry slot)
        | "ordinal" -> OrdinalModel assumptions
        | "residual" -> ResidualOnly
        | _ -> DeterministicInclusion assumptions

    let private decodeGuaranteeSlot (root: obj) (slot: string) : (string * CertificateGuarantee) option =
        let entry = fieldOf root slot

        if isNullish entry then
            None
        else
            Some(slot, decodeGuaranteeKind entry slot)

    let private decodeGuarantees (certificate: obj) : Map<string, CertificateGuarantee> =
        let root = fieldOf certificate "guarantees"

        if isNullish root then
            Map.empty
        else
            [ "exact"; "bound"; "sample"; "ordinal"; "latent"; "residual" ]
            |> List.choose (decodeGuaranteeSlot root)
            |> Map.ofList

    let private singlePayload (certificate: obj) (names: string list) : obj option =
        names
        |> List.tryPick (fun name ->
            let entry = fieldOf certificate name

            if isNullish entry then None else Some entry)

    let private boundObjectEntry (bound: obj) (side: string) : obj =
        let isObject: bool = emitJsExpr bound "typeof $0 === 'object'"
        if isObject then fieldOf bound side else bound

    let private boundFallback (certificate: obj) (side: string) : obj option =
        let bound = fieldOf certificate "bound"

        if isNullish bound || isJsArray bound then None
        elif isNullish (boundObjectEntry bound side) then None
        else Some(boundObjectEntry bound side)

    let private boundSide (certificate: obj) (direct: string list) (side: string) : obj option =
        match singlePayload certificate direct with
        | Some entry -> Some entry
        | None -> boundFallback certificate side

    let private splitOrdinalSource (source: obj) : obj list =
        let items = unbox<obj array> source |> Array.toList

        match items with
        | [] -> []
        | first :: _ when isJsArray first -> items
        | _ -> [ source ]

    let private decodeOrdinal (certificate: obj) : obj list =
        let stored = fieldOf certificate "ordinalConstraints"

        let source =
            if isNullish stored then
                fieldOf certificate "ordinal"
            else
                stored

        if isNullish source then []
        elif isJsArray source then splitOrdinalSource source
        else [ source ]

    let private boundNumbersOf (low: obj) (high: obj) : Result<float * float, CertFault> =
        match numberOf low, numberOf high with
        | Some lowNumber, Some highNumber -> Ok(lowNumber, highNumber)
        | _ -> Error(CertFault.Upstream("invalid-patch", "bound slot carries non-numeric envelopes"))

    let private decodeResidualNumber (payload: obj) : Result<float, CertFault> =
        match numberOf payload with
        | Some number -> Ok number
        | None -> Error(CertFault.Upstream("invalid-patch", "residual slot carries a non-numeric value"))

    let private siblingPatches
        (certificate: obj)
        (guarantees: Map<string, CertificateGuarantee>)
        : Result<CertificatePatchRequest list, CertFault> =
        let emptyIds: EventId list = []

        let baseOf (slot: string) : CertificatePatchRequest =
            { Slot = slot
              Value = None
              Lower = None
              Upper = None
              Summary = None
              Constraints = None
              Posterior = None
              ResidualValue = None
              GuaranteeKind = None
              Level = None
              Error = None
              Assumptions = None
              Scope = None
              Witnesses = emptyIds
              Derivations = emptyIds }

        let inclusion name =
            match Map.tryFind name guarantees with
            | Some(DeterministicInclusion assumptions) -> Some(assumptions |> Set.toArray)
            | _ -> None

        let coverage name =
            match Map.tryFind name guarantees with
            | Some(ProbabilisticCoverage(level, margin, assumptions, scope)) ->
                level, margin, Some(assumptions |> Set.toArray), Some scope
            | _ -> 0.95, 0.0, Some [| "guarantee-not-recorded" |], Some name

        let exactPart =
            match singlePayload certificate [ "exact" ] with
            | None -> Ok []
            | Some payload ->
                Ok
                    [ { baseOf "exact" with
                          Value = Some payload
                          GuaranteeKind = Some "inclusion"
                          Assumptions = inclusion "exact" } ]

        let boundPart =
            let lower = boundSide certificate [ "lowerEnvelope"; "lower" ] "lower"
            let upper = boundSide certificate [ "upperEnvelope"; "upper" ] "upper"

            match lower, upper with
            | None, None -> Ok []
            | Some low, Some high ->
                boundNumbersOf low high
                |> Result.map (fun (lowNumber, highNumber) ->
                    [ { baseOf "bound" with
                          Lower = Some lowNumber
                          Upper = Some highNumber
                          GuaranteeKind = Some "inclusion"
                          Assumptions = inclusion "bound" } ])
            | _ -> Error(CertFault.Upstream("invalid-patch", "bound slot needs both lower and upper envelopes"))

        let samplePart =
            match singlePayload certificate [ "sampleSummary"; "sample" ] with
            | None -> Ok []
            | Some payload ->
                let level, margin, assumptions, scope = coverage "sample"

                Ok
                    [ { baseOf "sample" with
                          Summary = Some payload
                          GuaranteeKind = Some "coverage"
                          Level = Some level
                          Error = Some margin
                          Assumptions = assumptions
                          Scope = scope } ]

        let ordinalPart =
            let assumptions =
                match Map.tryFind "ordinal" guarantees with
                | Some(OrdinalModel names) -> Some(names |> Set.toArray)
                | _ -> None

            Ok(
                decodeOrdinal certificate
                |> List.map (fun payload ->
                    { baseOf "ordinal" with
                        Constraints = Some payload
                        GuaranteeKind = Some "ordinal"
                        Assumptions = assumptions })
            )

        let latentPart =
            match singlePayload certificate [ "latentPosterior"; "latent" ] with
            | None -> Ok []
            | Some payload ->
                let level, margin, assumptions, scope = coverage "latent"

                Ok
                    [ { baseOf "latent" with
                          Posterior = Some payload
                          GuaranteeKind = Some "coverage"
                          Level = Some level
                          Error = Some margin
                          Assumptions = assumptions
                          Scope = scope } ]

        let residualPart =
            match singlePayload certificate [ "residual" ] with
            | None -> Ok []
            | Some payload ->
                decodeResidualNumber payload
                |> Result.map (fun number ->
                    [ { baseOf "residual" with
                          ResidualValue = Some number } ])

        [ exactPart; boundPart; samplePart; ordinalPart; latentPart; residualPart ]
        |> List.fold
            (fun result part ->
                result
                |> Result.bind (fun patches -> part |> Result.map (fun group -> patches @ group)))
            (Ok [])

    let private revisionOf (certificate: obj) : int64 =
        match floatField certificate "revision" with
        | Some number -> int64 number
        | None -> 0L

    let private decodeCertificate (certificate: obj) : Result<DecodedCertificate, CertFault> =
        let rawNode = fieldOf certificate "nodeId"

        let nodeText =
            if isNullish rawNode then
                textOf (fieldOf certificate "node")
            else
                textOf rawNode

        match NodeId.tryCreate nodeText with
        | Error _ -> Error(CertFault.InvalidNode nodeText)
        | Ok node ->
            siblingPatches certificate (decodeGuarantees certificate)
            |> Result.map (fun siblings ->
                { NodeId = node
                  Siblings = siblings
                  Witnesses = decodeEventIds certificate "witnesses"
                  Derivations = decodeEventIds certificate "derivations"
                  Revision = revisionOf certificate })

    let private decodePatchSlot (patch: obj) : Result<string, CertFault> =
        let slot = textOf (fieldOf patch "slot")

        if String.IsNullOrWhiteSpace slot then
            Error(CertFault.Upstream("invalid-patch", "certificate patch names an unknown slot"))
        else
            Ok slot

    let private decodePatchAssumptions (guarantee: obj) : string[] option =
        let raw = fieldOf guarantee "assumptions"

        if isNullish raw then
            None
        else
            Some(
                stringArrayOf raw
                |> List.filter (not << String.IsNullOrWhiteSpace)
                |> List.toArray
            )

    let private decodePatchScope (patch: obj) (guarantee: obj) : string option =
        let scopeText = textOf (fieldOf guarantee "scope")
        let direct = textOf (fieldOf patch "scope")

        match scopeText, direct with
        | scope, _ when not (String.IsNullOrWhiteSpace scope) -> Some scope
        | _, direct when not (String.IsNullOrWhiteSpace direct) -> Some direct
        | _ -> None

    let private decodePatch (patch: obj) : Result<CertificatePatchRequest, CertFault> =
        if isNullish patch then
            Error(CertFault.Upstream("invalid-patch", "certificate patch is missing"))
        else
            decodePatchSlot patch
            |> Result.map (fun slot ->
                let guarantee = fieldOf patch "guarantee"
                let kindText = textOf (fieldOf guarantee "kind")

                let valueOf name =
                    let entry = fieldOf patch name

                    if isNullish entry then None else Some entry

                { Slot = slot
                  Value = valueOf "value"
                  Lower = floatField patch "lower"
                  Upper = floatField patch "upper"
                  Summary = valueOf "summary"
                  Constraints = valueOf "constraints"
                  Posterior = valueOf "posterior"
                  ResidualValue = floatField patch "value"
                  GuaranteeKind = Option.ofObj kindText |> Option.filter (not << String.IsNullOrWhiteSpace)
                  Level = floatField guarantee "level"
                  Error = floatField guarantee "error"
                  Assumptions = decodePatchAssumptions guarantee
                  Scope = decodePatchScope patch guarantee
                  Witnesses = decodeEventIds patch "witnesses"
                  Derivations = decodeEventIds patch "derivations" })

    let private rebuildBase (decoded: DecodedCertificate) : Result<ValueCertificate, CertFault> =
        let seed =
            { Certificate.empty decoded.NodeId with
                WitnessEvents = decoded.Witnesses
                DerivationEvents = decoded.Derivations
                Revision = decoded.Revision }

        decoded.Siblings
        |> List.fold
            (fun result patch ->
                result
                |> Result.bind (fun current ->
                    Certificate.apply current patch
                    |> Result.mapError (fun fault -> CertFault.Upstream(fault.Code, fault.Message))))
            (Ok seed)

    let private envelopePayload (entry: JsonEnvelope option) : obj =
        match entry with
        | Some envelope -> JS.JSON.parse envelope.CanonicalPayload
        | None -> null

    let private guaranteeView (slot: string) (guarantee: CertificateGuarantee) : string * obj =
        match guarantee with
        | DeterministicInclusion assumptions ->
            slot,
            box
                {| kind = "inclusion"
                   assumptions = assumptions |> Set.toArray |> box |}
        | ProbabilisticCoverage(level, margin, assumptions, scope) ->
            slot,
            box
                {| kind = "coverage"
                   level = level
                   error = margin
                   assumptions = assumptions |> Set.toArray |> box
                   scope = scope |}
        | OrdinalModel assumptions ->
            slot,
            box
                {| kind = "ordinal"
                   assumptions = assumptions |> Set.toArray |> box |}
        | ResidualOnly -> slot, box {| kind = "residual" |}

    let private certificateView (certificate: ValueCertificate) : obj =
        let node = NodeId.value certificate.NodeId
        let lower = envelopePayload certificate.LowerEnvelope
        let upper = envelopePayload certificate.UpperEnvelope
        let exact = envelopePayload certificate.Exact
        let sample = envelopePayload certificate.SampleSummary
        let latent = envelopePayload certificate.LatentPosterior
        let residual = envelopePayload certificate.Residual

        let ordinalSingle =
            match
                certificate.OrdinalConstraints
                |> List.map (fun entry -> JS.JSON.parse entry.CanonicalPayload)
            with
            | [ single ] -> single
            | [] -> null
            | many -> many |> List.toArray |> box

        box
            {| node = node
               nodeId = node
               exact = exact
               lower = lower
               lowerEnvelope = lower
               bound = box {| lower = lower; upper = upper |}
               upper = upper
               upperEnvelope = upper
               sample = sample
               sampleSummary = sample
               ordinal = ordinalSingle
               ordinalConstraints = ordinalSingle
               latent = latent
               latentPosterior = latent
               residual = residual
               guarantees =
                certificate.Guarantees
                |> Map.toList
                |> List.map (fun (slot, guarantee) -> guaranteeView slot guarantee)
                |> createObj
               witnesses = certificate.WitnessEvents |> List.map EventId.value |> List.toArray |> box
               derivations = certificate.DerivationEvents |> List.map EventId.value |> List.toArray |> box
               revision = float certificate.Revision |}

    let private applyRefinement
        (decoded: DecodedCertificate)
        (request: CertificatePatchRequest)
        : Result<ValueCertificate, CertFault> =
        rebuildBase decoded
        |> Result.bind (fun rebuilt ->
            Certificate.apply rebuilt request
            |> Result.mapError (fun fault -> CertFault.Upstream(fault.Code, fault.Message))
            |> Result.map (fun advanced ->
                { advanced with
                    Revision = decoded.Revision + 1L }))

    let private refineSlot (certificate: obj) (patch: obj) : obj =
        let outcome =
            decodeCertificate certificate
            |> Result.bind (fun decoded -> decodePatch patch |> Result.bind (applyRefinement decoded))

        match outcome with
        | Ok advanced -> okResult [ "certificate", certificateView advanced ]
        | Error fault -> typedError (certFaultCode fault) (certFaultMessage fault)

    let private bayesExact (state: obj) (patch: obj) : obj =
        let names =
            stringArrayOf (fieldOf state "hypotheses")
            |> List.filter (not << String.IsNullOrWhiteSpace)

        let priors = floatMapKeep (fieldOf state "priors")

        let hypotheses: BayesExact.Hypothesis list =
            names
            |> List.map (fun name ->
                { Key = name
                  Prior = priors |> Map.tryFind name |> Option.defaultValue 0.0 })

        let factors: BayesExact.Factor list =
            arrayOf (fieldOf patch "factors")
            |> Array.toList
            |> List.map (fun entry ->
                let key = textOf (fieldOf entry "dependencyKey")

                { SemanticKey = key
                  DependencyKey = key
                  Likelihoods = floatMapKeep (fieldOf entry "likelihoods")
                  Qualified = true })

        match BayesExact.infer hypotheses factors with
        | Ok posterior ->
            okResult
                [ "posterior", mapView posterior.Probabilities
                  "usedFactors", posterior.UsedFactors |> List.toArray |> box
                  "logPartition", box posterior.LogPartition ]
        | Error fault -> typedError (BayesExact.code fault) (BayesExact.message fault)

    let private astarOutcomeView (outcome: AStarRefiner.Outcome) : obj =
        match outcome with
        | AStarRefiner.Outcome.Optimal proof ->
            okResult
                [ "path", proof.Path |> List.toArray |> box
                  "cost", box proof.Cost
                  "expanded", proof.Expanded |> List.toArray |> box
                  "lowerBound", box proof.LowerBound
                  "upperBound", box proof.UpperBound
                  "assumptions", proof.Assumptions |> Set.toArray |> box ]
        | AStarRefiner.Outcome.Unreachable _ ->
            typedError "unreachable" "astar exhausted the frontier without reaching the goal"

    let private astarSolve (patch: obj) : obj =
        let edges: AStarRefiner.Edge list =
            arrayOf (fieldOf patch "edges")
            |> Array.toList
            |> List.map (fun entry ->
                { FromNode = textOf (fieldOf entry "from")
                  ToNode = textOf (fieldOf entry "to")
                  Cost = floatField entry "cost" |> Option.defaultValue Double.NaN })

        let problem: AStarRefiner.Problem =
            { Start = textOf (fieldOf patch "start")
              Goal = textOf (fieldOf patch "goal")
              Edges = edges
              Heuristic = floatMapKeep (fieldOf patch "heuristic") }

        match AStarRefiner.solve problem with
        | Error fault -> typedError (AStarRefiner.code fault) (AStarRefiner.message fault)
        | Ok outcome -> astarOutcomeView outcome

    let rec private walkPath (children: Map<string, string list>) (visited: Set<string>) (node: string) : int =
        match Set.contains node visited, Map.tryFind node children with
        | true, _ -> 0
        | false, None -> 0
        | false, Some moves ->
            moves
            |> List.filter (fun move -> not (Set.contains move visited))
            |> List.fold (fun best move -> max best (1 + walkPath children (Set.add node visited) move)) 0

    let private longestPath (children: Map<string, string list>) (root: string) : int =
        max 1 (walkPath children Set.empty root)

    let private mctsSample (patch: obj) : obj =
        let root = textOf (fieldOf patch "root")
        let children = stringListMapOf (fieldOf patch "children")
        let rewards = floatMapKeep (fieldOf patch "terminalReward")

        let iterations =
            floatField patch "iterations" |> Option.map int |> Option.defaultValue 100

        let seed = floatField patch "seed" |> Option.map int |> Option.defaultValue 0
        let delta = floatField patch "delta" |> Option.defaultValue 0.05

        // The kernel fills unlisted moves with 0.0, so the declared range
        // must cover that default as well as every supplied reward.
        let rewardLo, rewardHi =
            match rewards |> Map.toList |> List.map snd with
            | [] -> 0.0, 0.0
            | values -> 0.0 :: values |> List.min, 0.0 :: values |> List.max

        let actions =
            if Map.containsKey root children then
                children
            else
                Map.add root [] children

        let transitions: MctsRefiner.KernelEntry list =
            actions
            |> Map.toList
            |> List.collect (fun (state, moves) ->
                moves
                |> List.map (fun move ->
                    let reward = rewards |> Map.tryFind move |> Option.defaultValue 0.0

                    { State = state
                      Action = move
                      Outcomes =
                        [ { Next = move
                            Probability = 1.0
                            Reward = reward } ] }))

        let model: MctsRefiner.GenerativeModel =
            { Root = root
              Actions = actions
              Transitions = transitions
              Horizon = longestPath children root
              Discount = 1.0
              RewardLo = rewardLo
              RewardHi = rewardHi }

        let config: MctsRefiner.SearchConfig =
            { Iterations = iterations
              Exploration = sqrt 2.0
              Delta = delta
              Seed = seed
              DagSafe = false }

        match MctsRefiner.run model config with
        | Error fault -> typedError (MctsRefiner.code fault) (MctsRefiner.message fault)
        | Ok result ->
            okResult
                [ "estimates",
                  result.ActionStats
                  |> List.map (fun stats -> stats.Action ==> stats.Mean)
                  |> createObj
                  "coverage",
                  box
                      {| delta = result.Coverage.Delta
                         scope = result.Coverage.Scope
                         iterations = result.Coverage.Iterations
                         horizon = result.Coverage.Horizon
                         discount = result.Coverage.Discount
                         rewardLo = result.Coverage.RewardLo
                         rewardHi = result.Coverage.RewardHi
                         dagSafe = result.Coverage.DagSafe |}
                  "guarantee",
                  box (
                      sprintf
                          "descriptive sample summary over %d seeded iterations (seed %d); per-action radii are iid-idealization references with no finite-sample coverage under adaptive sampling; a legacy prior field is accepted but ignored (no PUCT in this refiner)"
                          iterations
                          seed
                  )
                  "seed", box seed ]

    let private ballotRanksOf (items: obj list) : string list list =
        match items with
        | [] -> []
        | first :: _ when isJsArray first -> items |> List.map stringArrayOf
        | _ -> items |> List.map (fun (label: obj) -> [ textOf label ])

    let private decodeBallot (entry: obj) : OrdinalInference.Ballot =
        if not (isJsArray entry) then
            { Ranks = [] }
        else
            { Ranks = ballotRanksOf (unbox<obj array> entry |> Array.toList) }

    let private borda (input: obj) : obj =
        let request: OrdinalInference.BordaInput =
            { Candidates = stringArrayOf (fieldOf input "candidates")
              Ballots = arrayOf (fieldOf input "ballots") |> Array.toList |> List.map decodeBallot }

        match OrdinalInference.borda request with
        | Error fault ->
            let code = OrdinalInference.bordaErrorCode fault
            typedError code code
        | Ok outcome ->
            okResult
                [ "scores", mapView outcome.Scores
                  "meanScores", mapView outcome.MeanScores
                  "ranking", outcome.Ranking |> List.toArray |> box
                  "exposure",
                  outcome.Exposure
                  |> Map.toList
                  |> List.map (fun (key, count) -> key ==> box count)
                  |> createObj
                  "extension", box outcome.Extension
                  "complete", box outcome.Complete
                  "guarantees", outcome.Guarantees |> List.toArray |> box ]

    let private bradleyTerry (input: obj) : obj =
        let contests: OrdinalInference.Contest list =
            arrayOf (fieldOf input "comparisons")
            |> Array.toList
            |> List.map (fun entry ->
                { First = textOf (fieldOf entry "a")
                  Second = textOf (fieldOf entry "b")
                  FirstWins = floatField entry "winsA" |> Option.map int |> Option.defaultValue 0
                  SecondWins = floatField entry "winsB" |> Option.map int |> Option.defaultValue 0 })

        let request: OrdinalInference.BtlInput =
            { Candidates = stringArrayOf (fieldOf input "candidates")
              Contests = contests
              Regularization = floatField input "regularization" |> Option.defaultValue 0.0
              Tolerance = 1e-8
              MaxIterations = 100 }

        match OrdinalInference.bradleyTerry request with
        | Error fault -> stringError (OrdinalInference.btlErrorCode fault)
        | Ok outcome ->
            okResult
                [ "strengths", mapView outcome.Strengths
                  "appearances",
                  outcome.Appearances
                  |> Map.toList
                  |> List.map (fun (key, count) -> key ==> box count)
                  |> createObj
                  "diagnostics",
                  box
                      {| iterations = outcome.Diagnostics.Iterations
                         converged = outcome.Diagnostics.Converged
                         logLikelihood = outcome.Diagnostics.LogLikelihood
                         gradientNorm = outcome.Diagnostics.GradientNorm
                         maxAbsStrength = outcome.Diagnostics.MaxAbsStrength
                         regularization = outcome.Diagnostics.Regularization |}
                  "uncertainty", box {| standardErrors = mapView outcome.Uncertainty.StandardErrors |}
                  "assumptions", outcome.Assumptions |> Set.toArray |> box ]

    let private refineKind (state: obj) (patch: obj) : obj =
        match textOf (fieldOf patch "kind") with
        | "bayes-exact" -> bayesExact state patch
        | "astar" -> astarSolve patch
        | "mcts-sample" -> mctsSample patch
        | kind -> typedError "unknown-kind" (sprintf "refinement kind is not supported: %s" kind)

    let refineCertificate (first: obj) (second: obj) : obj = refineKind first second

    let private refineSlotEntry (input: obj) : obj =
        let certificate = fieldOf input "certificate"
        let patch = fieldOf input "patch"

        if isNullish certificate || isNullish patch then
            typedError "invalid-patch" "refineCertificate needs a certificate and a patch"
        else
            refineSlot certificate patch

    let private refineCertificateEntry: obj =
        emitJsExpr
            (refineSlotEntry, refineCertificate)
            "(...args) => args.length > 1 ? $1(args[0], args[1]) : $0(args[0])"

    let methods: (string * obj) list =
        [ "refineCertificate", refineCertificateEntry
          "borda", box borda
          "bradleyTerry", box bradleyTerry ]
