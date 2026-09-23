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

type DispatchDecision =
    { Dispatchable: WorkSpec list
      Excluded: (WorkSpec * AgendaExclusion) list }

module Agenda =

    let private error code message : Result<'value, AgendaError> =
        Error { Code = code; Message = message }

    /// A work item is dispatchable only when every dependency has actually succeeded.
    /// Membership of the currently selected set is not completion.
    let private blockingDependency (state: InquiryState) (spec: WorkSpec) : AgendaExclusion option =
        spec.Dependencies
        |> Set.toList
        |> List.sortBy WorkId.value
        |> List.tryPick (fun dependency ->
            match state.Work |> Map.tryFind dependency with
            | Some item ->
                match item.State with
                | WorkState.Succeeded _ -> None
                | _ -> Some(AgendaExclusion.MissingDependency dependency)
            | None -> Some(AgendaExclusion.MissingDependency dependency))

    /// Conflict keys keep two mutually exclusive measurements out of the same round.
    let private clashKey (used: Set<string>) (spec: WorkSpec) : AgendaExclusion option =
        spec.ConflictKeys
        |> Set.intersect used
        |> Set.toList
        |> List.sort
        |> List.tryHead
        |> Option.map AgendaExclusion.ConflictKeyClash

    /// Feasibility is checked against the ledger facts, not against a score.
    let private affordable
        (state: InquiryState)
        (alreadyReserved: Map<string, float>)
        (spec: WorkSpec)
        : AgendaExclusion option =
        let outstanding = Budget.mergeReserved [ alreadyReserved ]

        match
            Budget.tryReserve state.ResourceSpecs state.SettledUsage outstanding
                { WorkId = spec.Id
                  Attempt = spec.Attempt
                  Resources = Work.reserved spec
                  MoneyMinor = None }
        with
        | Ok _ -> None
        | Error fault -> Some(AgendaExclusion.ResourceExhausted fault.Code)

    /// Ready items are taken in a stable order (by work id) purely so the same inputs
    /// always produce the same batch; the order carries no value judgement.
    let planDispatch (state: InquiryState) (selected: WorkSpec list) (capacity: int) : DispatchDecision =
        let sorted =
            selected |> List.sortBy (fun spec -> WorkId.value spec.Id)

        let rec take (reserved: Map<string, float>) (used: Set<string>) (remaining: WorkSpec list) (granted: WorkSpec list) (blocked: (WorkSpec * AgendaExclusion) list) =
            match remaining with
            | [] -> { Dispatchable = List.rev granted; Excluded = List.rev blocked }
            | spec :: rest ->
                match state.Work |> Map.tryFind spec.Id with
                | Some item when Work.isTerminal item.State ->
                    take reserved used rest granted ((spec, AgendaExclusion.AlreadyTerminal) :: blocked)
                | _ ->
                    let reason =
                        blockingDependency state spec
                        |> Option.orElseWith (fun () -> clashKey used spec)
                        |> Option.orElseWith (fun () -> affordable state reserved spec)

                    let capacityReached = List.length granted >= capacity

                    match reason, capacityReached with
                    | Some cause, _ -> take reserved used rest granted ((spec, cause) :: blocked)
                    | None, true -> take reserved used rest granted ((spec, AgendaExclusion.CapacityUnavailable) :: blocked)
                    | None, false ->
                        let grown =
                            Work.reserved spec
                            |> Map.fold (fun acc key amount ->
                                let existing = acc |> Map.tryFind key |> Option.defaultValue 0.0
                                Map.add key (existing + amount) acc) reserved

                        take grown (Set.union used spec.ConflictKeys) rest (spec :: granted) blocked

        take Map.empty Set.empty sorted [] []
