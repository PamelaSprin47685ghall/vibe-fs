namespace Wanxiangshu.Sphinx.V2.Runtime

open Wanxiangshu.Sphinx.V2.Core

/// The Agenda answers one question: given a set of already-selected work items, which
/// of them may be dispatched *right now*.
///
/// WHAT[sphinx-v2-013]: the Agenda does not rank. It never looks at why a plan was
/// chosen and never assigns value. The old implementation filled a batch by sorting
/// targets by id and treating "selected in this batch" as dependency satisfaction, so
/// the first id in the batch won regardless of contribution and a dependent work could
/// be dispatched alongside its own prerequisite.

type AgendaError = { Code: string; Message: string }

type AgendaExclusion =
    | MissingDependency of dependency: WorkId
    | ConflictKeyClash of key: string
    | ResourceExhausted of resource: string
    | CapacityUnavailable
    | AlreadyTerminal

type DispatchVerdict =
    | Granted
    | Declined of AgendaExclusion
    | Terminal

type DispatchDecision =
    { Dispatchable: WorkSpec list
      Excluded: (WorkSpec * AgendaExclusion) list }

module Agenda =

    let private unmetDependency (state: InquiryState) (dependency: WorkId) : AgendaExclusion option =
        let existing = state.Work |> Map.tryFind dependency

        let missing () =
            Some(AgendaExclusion.MissingDependency dependency)

        let unmetItem (item: WorkItem) : AgendaExclusion option =
            match Work.isTerminal item.State with
            | true -> None
            | false -> missing ()

        match existing with
        | Some item -> unmetItem item
        | None -> missing ()

    let private blockingDependency (state: InquiryState) (spec: WorkSpec) : AgendaExclusion option =
        spec.Dependencies
        |> Set.toList
        |> List.sortBy WorkId.value
        |> List.tryPick (fun dependency -> unmetDependency state dependency)

    let private clashKey (used: Set<string>) (spec: WorkSpec) : AgendaExclusion option =
        let clash = Set.intersect used spec.ConflictKeys

        match Set.isEmpty clash with
        | true -> None
        | false ->
            clash
            |> Set.toList
            |> List.sort
            |> List.tryHead
            |> Option.map AgendaExclusion.ConflictKeyClash

    let private affordable
        (state: InquiryState)
        (reserved: Map<string, float>)
        (spec: WorkSpec)
        : AgendaExclusion option =
        let outstanding = Budget.mergeReserved [ reserved ]

        let attempt =
            Budget.tryReserve
                state.ResourceSpecs
                state.SettledUsage
                outstanding
                { WorkId = spec.Id
                  Attempt = spec.Attempt
                  Resources = Work.reserved spec
                  MoneyMinor = None }

        match attempt with
        | Ok _ -> None
        | Error fault -> Some(AgendaExclusion.ResourceExhausted fault.Code)

    /// The blocker, if any, for one spec at this point in the batch.
    let private blockerOf
        (state: InquiryState)
        (reserved: Map<string, float>)
        (used: Set<string>)
        (spec: WorkSpec)
        : AgendaExclusion option =
        blockingDependency state spec
        |> Option.orElseWith (fun () -> clashKey used spec)
        |> Option.orElseWith (fun () -> affordable state reserved spec)

    /// Carries out the verdict: grant takes the spec into the batch, decline records why.
    let private applyVerdict
        (take:
            Map<string, float>
                -> Set<string>
                -> WorkSpec list
                -> WorkSpec list
                -> (WorkSpec * AgendaExclusion) list
                -> DispatchDecision)
        (reserved: Map<string, float>)
        (used: Set<string>)
        (remaining: WorkSpec list)
        (granted: WorkSpec list)
        (blocked: (WorkSpec * AgendaExclusion) list)
        (spec: WorkSpec)
        (verdict: DispatchVerdict)
        : DispatchDecision =
        match verdict with
        | DispatchVerdict.Terminal ->
            take reserved used remaining granted ((spec, AgendaExclusion.AlreadyTerminal) :: blocked)
        | DispatchVerdict.Granted ->
            let grown =
                Work.reserved spec
                |> Map.fold
                    (fun acc key amount ->
                        let existing = acc |> Map.tryFind key |> Option.defaultValue 0.0
                        Map.add key (existing + amount) acc)
                    reserved

            take grown (Set.union used spec.ConflictKeys) remaining (spec :: granted) blocked
        | DispatchVerdict.Declined cause -> take reserved used remaining granted ((spec, cause) :: blocked)

    /// The verdict once terminality is ruled out: a blocker wins, otherwise capacity.
    let private blockerVerdict (blocker: AgendaExclusion option) (capacityReached: bool) : DispatchVerdict =
        let blocked (cause: AgendaExclusion) = DispatchVerdict.Declined cause

        let unblocked () =
            match capacityReached with
            | true -> DispatchVerdict.Declined AgendaExclusion.CapacityUnavailable
            | false -> DispatchVerdict.Granted

        match blocker with
        | Some cause -> blocked (Option.get blocker)
        | None -> unblocked ()

    /// One spec's verdict, and the batch continuation it implies.
    let private classifySpec
        (state: InquiryState)
        (spec: WorkSpec)
        (isTerminal: bool)
        (blocker: AgendaExclusion option)
        (capacityReached: bool)
        : DispatchVerdict =
        match isTerminal with
        | true -> DispatchVerdict.Terminal
        | false -> blockerVerdict blocker capacityReached

    /// Ready items are taken in a stable order (by work id) purely so the same inputs
    /// always produce the same batch; the order carries no value judgement.
    let planDispatch (state: InquiryState) (selected: WorkSpec list) (capacity: int) : DispatchDecision =
        let sorted = selected |> List.sortBy (fun spec -> WorkId.value spec.Id)

        let rec take
            (reserved: Map<string, float>)
            (used: Set<string>)
            (remaining: WorkSpec list)
            (granted: WorkSpec list)
            (blocked: (WorkSpec * AgendaExclusion) list)
            : DispatchDecision =
            match remaining with
            | [] ->
                { Dispatchable = List.rev granted
                  Excluded = blocked |> List.rev }
            | spec :: rest ->
                let isTerminal =
                    state.Work
                    |> Map.tryFind spec.Id
                    |> Option.map (fun item -> Work.isTerminal item.State)
                    |> Option.defaultValue false

                let blocker = blockerOf state reserved used spec
                let capacityReached = List.length granted >= capacity

                let verdict = classifySpec state spec isTerminal blocker capacityReached

                let applyTerminal () =
                    take reserved used rest granted ((spec, AgendaExclusion.AlreadyTerminal) :: blocked)

                let applyGranted () =
                    let grown =
                        Work.reserved spec
                        |> Map.fold
                            (fun acc key amount ->
                                let existing = acc |> Map.tryFind key |> Option.defaultValue 0.0
                                Map.add key (existing + amount) acc)
                            reserved

                    take grown (Set.union used spec.ConflictKeys) rest (spec :: granted) blocked

                let applyDeclined (cause: AgendaExclusion) =
                    take reserved used rest granted ((spec, cause) :: blocked)

                applyVerdict take reserved used rest granted blocked spec verdict

        take Map.empty Set.empty sorted [] []
