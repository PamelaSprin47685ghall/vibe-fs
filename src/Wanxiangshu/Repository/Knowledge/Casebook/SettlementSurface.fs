namespace Wanxiangshu.Repository.Knowledge.Casebook

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

module CasebookSettlementSurface =

    let private incidentOf (value: obj) =
        match value with
        | :? CasebookSemanticCutIncident as incident -> incident
        | _ -> invalidArg "incident" "A Casebook incident must come from its original settled owner"

    let private outcomeName (outcome: CasebookSettlementOutcome) =
        match outcome with
        | CasebookSettlementOutcome.Unscoped -> "Unscoped"
        | CasebookSettlementOutcome.CaseUnavailable -> "CaseUnavailable"
        | CasebookSettlementOutcome.CaseUnavailableKnown -> "CaseUnavailableKnown"
        | CasebookSettlementOutcome.LogScopeFailure -> "LogScopeFailure"

    /// The owner takes a reporting capability: it announces a conflict once and
    /// never terminates the host process.
    let createOwner (onConflict: obj -> unit) : obj =
        if isNull (box onConflict) then
            nullArg "onConflict"

        box (CasebookSettlementOwner(fun incident -> onConflict (box incident)))

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

    /// Re-observe an already settled incident: the owner keeps its single
    /// notification and its single archive.
    let observeIncident (owner: obj) (incident: obj) : obj =
        let incident = incidentOf incident
        let outcome = (unbox<CasebookSettlementOwner> owner).Redeliver incident
        box {| outcome = outcomeName outcome |}

    /// Observe one settled append failure and return the scope it had.
    let observeFailure (owner: obj) (failure: obj) : obj =
        let failure =
            match failure with
            | :? CasebookAppendFailure as failure -> failure
            | _ -> invalidArg "failure" "A Casebook settlement failure must be a CasebookAppendFailure"

        let outcome = (unbox<CasebookSettlementOwner> owner).Observe failure
        box {| outcome = outcomeName outcome |}
