namespace Wanxiangshu.Repository.Knowledge.Casebook

open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

module CasebookAppendSurface =

    let private eventIds events =
        events
        |> List.map (fun (event: EventEnvelope) -> EventId.value event.EventId)
        |> List.toArray

    let private faultToJs (fault: AppendFault) =
        box
            {| phase = string fault.Phase
               cause = fault.Cause |}

    let failureToJs (failure: CasebookAppendFailure) : obj =
        let fields =
            [ "operation" ==> string failure.Operation
              "caseIdentity" ==> failure.CaseIdentity
              "eventId" ==> EventId.value failure.EventId
              "code" ==> CasebookAppendFailure.code failure
              "isOriginalError"
              ==> (fun (expected: obj) -> obj.ReferenceEquals(box failure.Error, expected)) ]

        let evidence =
            match failure.Error with
            | AppendError.AppendNotAttempted value ->
                [ "kind" ==> "notAttempted"
                  "requestedEventIds" ==> eventIds value.Requested
                  "preparedEventIds"
                  ==> (value.Prepared |> Option.map (fun p -> eventIds p.DurableEvents) |> Option.toObj)
                  "primary" ==> faultToJs value.Primary
                  "cleanupFailures"
                  ==> (value.CleanupFailures |> List.map faultToJs |> List.toArray) ]
            | AppendError.CommitUnknown value ->
                [ "kind" ==> "unknown"
                  "requestedEventIds" ==> eventIds value.Requested
                  "preparedEventIds" ==> eventIds value.Prepared.DurableEvents
                  "primary" ==> faultToJs value.Primary
                  "cleanupFailures"
                  ==> (value.CleanupFailures |> List.map faultToJs |> List.toArray) ]
            | AppendError.NoNewWriteReleaseFailed value ->
                [ "kind" ==> "noNewWriteReleaseFailed"
                  "requestedEventIds" ==> eventIds value.Requested
                  "preparedEventIds"
                  ==> (value.Prepared |> Option.map (fun p -> eventIds p.DurableEvents) |> Option.toObj)
                  "primary"
                  ==> box
                          {| phase = "StoreRelease"
                             cause = value.Cause |}
                  "cleanupFailures" ==> ([||]: obj array) ]
            | _ -> [ "kind" ==> "rejected" ]

        createObj (fields @ evidence)

    let mutationErrorToJs (error: CasebookMutationError) : obj =
        match error with
        | CasebookMutationError.AppendFailure failure ->
            box
                {| ok = false
                   error = CasebookMutationError.describe error
                   code = CasebookAppendFailure.code failure
                   persistenceFailure = failureToJs failure |}
        | _ ->
            box
                {| ok = false
                   error = CasebookMutationError.describe error |}

    let finalizeFailureToJs (failure: CasebookAppendFailure) : obj =
        box
            {| ok = false
               kind = CasebookAppendFailure.finalizeKind failure
               error = CasebookMutationError.describe (CasebookMutationError.AppendFailure failure)
               code = CasebookAppendFailure.code failure
               persistenceFailure = failureToJs failure |}
