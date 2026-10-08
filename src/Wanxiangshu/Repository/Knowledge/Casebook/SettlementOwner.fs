namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Collections.Generic
open Wanxiangshu.Foundation.Identity

[<Sealed>]
type CasebookSettlementOwner(onFatal: CasebookSemanticCutIncident -> unit) =
    let gate = obj ()
    // DSL-MUTABLE: resource — delivery latch for settled incidents.
    let incidents = Dictionary<EventId, CasebookSemanticCutIncident>()

    do
        if isNull (box onFatal) then
            nullArg "onFatal"

    let claim (candidate: CasebookSemanticCutIncident) =
        lock gate (fun () ->
            match incidents.TryGetValue candidate.Failure.EventId with
            | true, existing -> existing, false
            | false, _ ->
                incidents.Add(candidate.Failure.EventId, candidate)
                candidate, true)

    let deliver candidate =
        let incident, firstDelivery = claim candidate

        if firstDelivery then
            onFatal incident

        raise incident

    member _.Observe(failure: CasebookAppendFailure) =
        match CasebookSemanticCutIncident.tryFromSettlement failure with
        | None -> ()
        | Some candidate -> deliver candidate

    member _.Redeliver(incident: CasebookSemanticCutIncident) : unit =
        let owned =
            lock gate (fun () ->
                match incidents.TryGetValue incident.Failure.EventId with
                | true, existing -> obj.ReferenceEquals(existing, incident)
                | false, _ -> false)

        if not owned then
            invalidArg "incident" "A Casebook incident must come from its original settled owner"

        raise incident
