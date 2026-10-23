namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Collections.Generic
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

/// Owner of the Casebook settlement outcomes.
///
/// The case owns its own archive, refresh and invalidation result: a conflict
/// that affects one case makes that case unavailable and is reported once. It
/// never terminates the host process, never revokes finished engineering work
/// and never discards saved artifacts. A failure of the shared event log keeps
/// its own scope: appends to that log stop and the failure stays a failure.
[<Sealed>]
type CasebookSettlementOwner(onConflict: CasebookSemanticCutIncident -> unit) =
    let gate = obj ()
    // DSL-MUTABLE: resource — settlement ledger for the conflicts this owner saw.
    let conflicts = Dictionary<EventId, CasebookSemanticCutIncident>()

    do
        if isNull (box onConflict) then
            nullArg "onConflict"

    let claim (candidate: CasebookSemanticCutIncident) =
        lock gate (fun () ->
            match conflicts.TryGetValue candidate.Failure.EventId with
            | true, existing -> existing, false
            | false, _ ->
                conflicts.Add(candidate.Failure.EventId, candidate)
                candidate, true)

    /// A reporting capability that fails must not change the settlement the
    /// caller receives: the conflict itself is already recorded.
    let report (incident: CasebookSemanticCutIncident) =
        try
            onConflict incident
        with _ ->
            ()

    let settleStorageInvalid (failure: CasebookAppendFailure) =
        // The shared log's own integrity is broken. Appends to it stop at
        // the store; this owner reports the scope and never claims the
        // case was written.
        let incident, firstDelivery = claim (CasebookSemanticCutIncident.OfFailure failure)

        if firstDelivery then
            report incident

        CasebookSettlementOutcome.LogScopeFailure

    let settleCandidate (candidate: CasebookSemanticCutIncident) =
        let _, firstDelivery = claim candidate

        if firstDelivery then
            report candidate
            CasebookSettlementOutcome.CaseUnavailable
        else
            CasebookSettlementOutcome.CaseUnavailableKnown

    let settleSemantic (failure: CasebookAppendFailure) =
        match CasebookSemanticCutIncident.tryFromSettlement failure with
        | None -> CasebookSettlementOutcome.Unscoped
        | Some candidate -> settleCandidate candidate

    member _.Observe(failure: CasebookAppendFailure) : CasebookSettlementOutcome =
        match failure.Error with
        | AppendError.StorageInvalid _ -> settleStorageInvalid failure
        | _ -> settleSemantic failure

    /// Re-observe an incident this owner already settled: no new notification
    /// and no new archive.
    member _.Redeliver(incident: CasebookSemanticCutIncident) : CasebookSettlementOutcome =
        let owned =
            lock gate (fun () ->
                match conflicts.TryGetValue incident.Failure.EventId with
                | true, existing -> obj.ReferenceEquals(existing, incident)
                | false, _ -> false)

        if not owned then
            invalidArg "incident" "A Casebook incident must come from its original settled owner"

        CasebookSettlementOutcome.CaseUnavailableKnown
