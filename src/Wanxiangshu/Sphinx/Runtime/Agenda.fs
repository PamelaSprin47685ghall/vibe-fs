namespace Wanxiangshu.Sphinx.Runtime

open System

type RefinementTarget =
    { Id: string
      Dependencies: Set<string>
      ConflictKeys: Set<string>
      Cost: Map<string, float>
      LossCurrency: string option
      LossValue: float option
      CommonCurrency: string option
      EffectSlot: string option }

type ScheduleRequest =
    { Targets: RefinementTarget list
      Budget: Map<string, float>
      Completed: Set<string> }

type ScheduleResult =
    { Batch: string list
      Pareto: string list
      Order: string list }

type ScheduleError = { Code: string; Message: string }

type ClosureDomain =
    | FiniteDag of nodes: int * edges: (int * int) list
    | FiniteChain of monotone: bool * continuous: bool
    | MetricSpace of modulus: float
    | NoDomain

type ClosureOperator =
    | DagRecurrence of order: int list * seeds: Map<int, float> * rule: string
    | FiniteMap of start: int * table: int list
    | AffineMap of factor: float * offset: float * start: float
    | NoOperator

type AsyncExpectation =
    { FiniteDecisionSet: bool
      StrictGap: bool
      VanishingUncertainty: bool
      FairScheduling: bool
      OrderAware: bool
      CorrectSpecification: bool option }

type ClosureRequest =
    { Domain: ClosureDomain option
      Operator: ClosureOperator option
      MaxIterations: int
      Async: AsyncExpectation option }

type FixedPoint =
    | DagPoint of Map<int, float>
    | ScalarPoint of float
    | NoPoint

type ClosureOutcome =
    { Converged: bool
      Point: FixedPoint
      Iterations: int
      ResidualBound: float
      Unique: bool }

module Agenda =

    let private error code message =
        Error { Code = code; Message = message }

    let private isFiniteNumber (value: float) : bool =
        not (Double.IsNaN value || Double.IsInfinity value)

    let private compareOrdinal left right =
        String.Compare(left, right, StringComparison.Ordinal)

    let private lossOf target =
        match target.LossCurrency, target.LossValue with
        | Some currency, Some value when isFiniteNumber value -> Some(currency, value)
        | _ -> None

    let private shareCommonCurrency (left: RefinementTarget) (right: RefinementTarget) : bool =
        match left.CommonCurrency, right.CommonCurrency with
        | Some declaredLeft, Some declaredRight when
            declaredLeft = declaredRight && not (String.IsNullOrWhiteSpace declaredLeft)
            ->
            true
        | _ -> false

    let private comparable (left: RefinementTarget) (right: RefinementTarget) : bool =
        match lossOf left, lossOf right with
        | Some(currencyLeft, _), Some(currencyRight, _) when currencyLeft = currencyRight -> true
        | Some _, Some _ -> shareCommonCurrency left right
        | _ -> false

    let private dominates winner loser =
        winner.Id <> loser.Id
        && comparable winner loser
        && (match winner.LossValue, loser.LossValue with
            | Some winnerLoss, Some loserLoss -> winnerLoss < loserLoss
            | _ -> false)

    let frontier targets =
        targets
        |> List.filter (fun candidate -> not (targets |> List.exists (fun other -> dominates other candidate)))
        |> List.map (fun target -> target.Id)
        |> List.sortWith compareOrdinal

    let private validTarget target =
        not (String.IsNullOrWhiteSpace target.Id)
        && (target.Cost
            |> Map.forall (fun _ amount -> isFiniteNumber amount && amount >= 0.0))
        && (target.LossValue |> Option.forall isFiniteNumber)

    let private fitsBudget (budget: Map<string, float>) (totals: Map<string, float>) (target: RefinementTarget) : bool =
        target.Cost
        |> Map.forall (fun resource amount ->
            let available = budget |> Map.tryFind resource |> Option.defaultValue 0.0
            let spent = totals |> Map.tryFind resource |> Option.defaultValue 0.0
            amount <= available - spent)

    let private readyFor (completed: Set<string>) (selected: Set<string>) (target: RefinementTarget) : bool =
        target.Dependencies
        |> Set.forall (fun dependency -> Set.contains dependency completed || Set.contains dependency selected)

    let private fillBatch
        (ordered: RefinementTarget list)
        (budget: Map<string, float>)
        (completed: Set<string>)
        : Set<string> =
        let rec fill (selected: Set<string>) (conflicts: Set<string>) (totals: Map<string, float>) =
            let nextSelected, nextConflicts, nextTotals, moved =
                ordered
                |> List.fold
                    (fun (chosen, used, spent, progressed) (target: RefinementTarget) ->
                        if Set.contains target.Id chosen then
                            (chosen, used, spent, progressed)
                        elif not (readyFor completed chosen target) then
                            (chosen, used, spent, progressed)
                        elif not (Set.intersect target.ConflictKeys used |> Set.isEmpty) then
                            (chosen, used, spent, progressed)
                        elif not (fitsBudget budget spent target) then
                            (chosen, used, spent, progressed)
                        else
                            let grown =
                                target.Cost
                                |> Map.fold
                                    (fun current resource amount ->
                                        let paid = current |> Map.tryFind resource |> Option.defaultValue 0.0

                                        Map.add resource (paid + amount) current)
                                    spent

                            (Set.add target.Id chosen, Set.union used target.ConflictKeys, grown, true))
                    (selected, conflicts, totals, false)

            if moved then
                fill nextSelected nextConflicts nextTotals
            else
                nextSelected

        fill Set.empty Set.empty Map.empty

    let schedule (request: ScheduleRequest) =
        let ids = request.Targets |> List.map (fun (target: RefinementTarget) -> target.Id)

        if request.Targets |> List.exists (fun target -> not (validTarget target)) then
            error "invalid-target" "refinement targets need nonblank ids with finite nonnegative costs"
        elif List.length ids <> (ids |> Set.ofList |> Set.count) then
            error "invalid-target" "refinement ids must be unique"
        elif
            request.Budget
            |> Map.exists (fun _ amount -> not (isFiniteNumber amount) || amount < 0.0)
        then
            error "invalid-budget" "schedule budget must be finite and nonnegative"
        else
            let ordered =
                request.Targets
                |> List.sortWith (fun left right -> compareOrdinal left.Id right.Id)

            let batch =
                fillBatch ordered request.Budget request.Completed
                |> Set.toList
                |> List.sortWith compareOrdinal

            Ok
                { Batch = batch
                  Pareto = frontier request.Targets
                  Order = batch }

    let private asyncProven expectation =
        match expectation with
        | None -> true
        | Some required ->
            required.FiniteDecisionSet
            && required.StrictGap
            && required.VanishingUncertainty
            && required.FairScheduling
            && required.OrderAware
            && (required.CorrectSpecification |> Option.defaultValue true)

    let private unclaimed iterations residual =
        { Converged = false
          Point = NoPoint
          Iterations = iterations
          ResidualBound = residual
          Unique = false }

    let private enqueueDagSuccessor (head: int) (queueInner: int list) (updated: Map<int, int>) (count: int) =
        if count = 0 then
            (head :: queueInner, updated)
        else
            (queueInner, updated)

    let rec private drainDagQueue
        (successors: Map<int, int list>)
        (nodes: int)
        (queue: int list)
        (remaining: Map<int, int>)
        (seen: int)
        : bool =
        match queue with
        | [] -> seen = nodes
        | node :: rest ->
            let directed = Map.tryFind node successors |> Option.defaultValue []

            let nextQueue, nextRemaining =
                directed
                |> List.fold
                    (fun (queueInner, remainingInner) head ->
                        let count = (Map.find head remainingInner) - 1
                        let updated = Map.add head count remainingInner

                        enqueueDagSuccessor head queueInner updated count)
                    (rest, remaining)

            drainDagQueue successors nodes nextQueue nextRemaining (seen + 1)

    let private dagAcyclic nodes edges =
        if nodes <= 0 then
            false
        elif
            edges
            |> List.exists (fun (fromNode, toNode) ->
                fromNode < 0 || toNode < 0 || fromNode >= nodes || toNode >= nodes)
        then
            false
        else
            let successors =
                edges
                |> List.groupBy fst
                |> Map.ofList
                |> Map.map (fun _ directed -> directed |> List.map snd)

            let initial =
                [ 0 .. nodes - 1 ]
                |> List.map (fun node -> node, edges |> List.filter (fun (_, head) -> head = node) |> List.length)
                |> Map.ofList

            let ready =
                initial
                |> Map.filter (fun _ count -> count = 0)
                |> Map.toList
                |> List.map fst
                |> List.sort

            drainDagQueue successors nodes ready initial 0

    let private dagKnownMax (current: Map<int, float>) (tails: int list) : float =
        let known = tails |> List.choose (fun tail -> Map.tryFind tail current)

        if List.isEmpty known then 0.0 else List.max known

    let private dagNodeValue
        (seeds: Map<int, float>)
        (predecessors: Map<int, int list>)
        (current: Map<int, float>)
        (node: int)
        : float =
        match Map.tryFind node seeds, Map.tryFind node predecessors with
        | Some seed, _ -> seed
        | None, None -> 0.0
        | None, Some tails -> dagKnownMax current tails + 1.0

    let private evalDag nodes edges order seeds rule maxIterations =
        let orderOk =
            List.length order = nodes && Set.ofList order = Set.ofList [ 0 .. nodes - 1 ]

        let seedsOk = seeds |> Map.forall (fun _ value -> isFiniteNumber value)

        if
            maxIterations <= 0
            || not orderOk
            || rule <> "max-pred-plus-one"
            || not seedsOk
            || not (dagAcyclic nodes edges)
        then
            unclaimed 0 1.0
        else
            let predecessors =
                edges
                |> List.groupBy snd
                |> Map.ofList
                |> Map.map (fun _ tails -> tails |> List.map fst)

            let values =
                order
                |> List.fold
                    (fun current node -> Map.add node (dagNodeValue seeds predecessors current node) current)
                    Map.empty

            { Converged = true
              Point = DagPoint values
              Iterations = 1
              ResidualBound = 0.0
              Unique = false }

    let private chainLookup (table: int list) (state: int) : int =
        match List.tryItem state table with
        | Some next -> next
        | None -> state

    let private chainStep (table: int list) (state: int) : int =
        if state >= 0 then chainLookup table state else state

    let private evalChain monotone continuous start table maxIterations =
        let rec run state iterations =
            let next = chainStep table state

            if iterations >= maxIterations then
                (state, iterations, false)
            elif next = state then
                (next, iterations + 1, true)
            else
                run next (iterations + 1)

        let finalState, iterations, stable =
            if maxIterations <= 0 then
                (start, 0, false)
            else
                run start 0

        let residual = float (abs (chainStep table finalState - finalState))

        if monotone && continuous && stable then
            { Converged = true
              Point = ScalarPoint(float finalState)
              Iterations = iterations
              ResidualBound = 0.0
              Unique = false }
        else
            unclaimed iterations residual

    let private evalAffine modulus factor offset start maxIterations =
        let gate =
            isFiniteNumber modulus
            && modulus >= 0.0
            && modulus < 1.0
            && isFiniteNumber factor
            && abs factor < 1.0
            && isFiniteNumber offset
            && isFiniteNumber start

        let step state = factor * state + offset

        let rec run state iterations =
            let next = step state

            if iterations >= maxIterations then
                (state, iterations, false)
            elif not (isFiniteNumber next) then
                (state, iterations, false)
            elif abs (next - state) < 1e-12 then
                (next, iterations + 1, true)
            else
                run next (iterations + 1)

        let finalState, iterations, stable =
            if maxIterations <= 0 then
                (start, 0, false)
            else
                run start 0

        let residual =
            let applied = step finalState

            if isFiniteNumber applied && isFiniteNumber finalState then
                abs (applied - finalState)
            else
                1.0

        if gate && stable then
            { Converged = true
              Point = ScalarPoint finalState
              Iterations = iterations
              ResidualBound = residual
              Unique = true }
        else
            unclaimed iterations residual

    let private downgradeUnproven (outcome: ClosureOutcome) : ClosureOutcome =
        match outcome with
        | { Converged = true } -> unclaimed outcome.Iterations outcome.ResidualBound
        | _ -> outcome

    let evaluateClosure (request: ClosureRequest) =
        let bound = max 0 request.MaxIterations

        let outcome =
            match request.Domain, request.Operator with
            | Some(FiniteDag(nodes, edges)), Some(DagRecurrence(order, seeds, rule)) ->
                evalDag nodes edges order seeds rule bound
            | Some(FiniteChain(monotone, continuous)), Some(FiniteMap(start, table)) ->
                evalChain monotone continuous start table bound
            | Some(MetricSpace modulus), Some(AffineMap(factor, offset, start)) ->
                evalAffine modulus factor offset start bound
            | _ -> unclaimed 0 1.0

        if asyncProven request.Async then
            outcome
        else
            downgradeUnproven outcome
