namespace Wanxiangshu.Repository.Knowledge.Casebook

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

module CasebookSettlementSurface =

    let private incidentOf (value: obj) =
        match value with
        | :? CasebookSemanticCutIncident as incident -> incident
        | _ -> invalidArg "incident" "A Casebook incident must come from its original settled owner"

    let createOwner (onIncident: obj -> unit) : obj =
        if isNull (box onIncident) then
            nullArg "onIncident"

        box (CasebookSettlementOwner(fun incident -> onIncident (box incident)))

    let describeIncident (incident: obj) : obj =
        let incident = incidentOf incident
        let failure = incident.Failure

        let sharesPreparedWithError (expectedError: obj) =
            match failure.Error, unbox<AppendError> expectedError with
            | AppendError.CommitUnknown actual, AppendError.CommitUnknown expected ->
                obj.ReferenceEquals(actual.Prepared, expected.Prepared)
            | _ -> false

        box
            {| operation = string failure.Operation
               caseIdentity = failure.CaseIdentity
               eventId = EventId.value failure.EventId
               cuts =
                incident.Cuts
                |> List.map (fun cut ->
                    {| rule = cut.Rule
                       failedEventId = EventId.value cut.FailedEventId
                       cutEventId = EventId.value cut.CutEventId
                       reason = cut.Reason |})
                |> List.toArray
               failure = CasebookAppendSurface.failureToJs failure
               sharesPreparedWithError = sharesPreparedWithError |}

    let isIncident (value: obj) : bool = value :? CasebookSemanticCutIncident

    let observeIncident (owner: obj) (incident: obj) : unit =
        let incident = incidentOf incident
        (unbox<CasebookSettlementOwner> owner).Redeliver incident
