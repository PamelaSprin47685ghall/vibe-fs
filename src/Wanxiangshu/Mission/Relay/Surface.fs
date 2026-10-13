namespace Wanxiangshu.Mission.Relay

open Fable.Core.JsInterop
open FsToolkit.ErrorHandling

module Surface =
    [<RequireQualifiedAccess>]
    type Admission =
        | Refused

    let empty () = Fold.empty

    let openRoad () : Admission =
        Admission.Refused

    let private result value =
        match value with
        | Ok state -> box {| ok = true; state = state |}
        | Error error -> box {| ok = false; error = error |}

    let openIncumbency () : Admission =
        Admission.Refused

    let assess () : Admission =
        Admission.Refused

    let advanceAuthority state road incumbent expected next authorityMessageId =
        Decision.advanceAuthority
            state
            (RoadId.create road)
            (IncumbencyId.create incumbent)
            (AuthorityRevision.create expected)
            (AuthorityRevision.create next)
            (PhysicalUserMessageId.create authorityMessageId)
        |> result

    let blockCleanup state road incumbent blockerDigest =
        Decision.blockCleanup state (RoadId.create road) (IncumbencyId.create incumbent) blockerDigest
        |> result

    let confirmRetirement () : Admission =
        Admission.Refused

    let private normalize (value: string) = if isNull value then "" else value

    let private activeRetirementContext state road incumbent =
        match Fold.view state (RoadId.create road) with
        | None -> Error "RoadNotOpen"
        | Some view ->
            match view.ActiveIncumbency, view.ActiveAuthorityRevision with
            | Some activeId, Some authorityRevision when activeId = IncumbencyId.create incumbent ->
                Ok authorityRevision
            | _ -> Error "IncumbencyNotActive"

    let retire state road incumbent retirement providerRun toolCall outcome =
        let parsed =
            match outcome with
            | "Relayed" -> Ok RetirementOutcome.Relayed
            | "Delivered" -> Ok RetirementOutcome.Delivered
            | _ -> Error "RetirementOutcomeUnknown"

        match parsed with
        | Error error ->
            box
                {| ok = false
                   error = error |}
        | Ok retirementOutcome ->
            match activeRetirementContext state road incumbent with
            | Error error -> box {| ok = false; error = error |}
            | Ok authorityRevision ->
                let summary =
                    { Id = RetirementId.create retirement
                      IncumbencyId = IncumbencyId.create incumbent
                      AuthorityRevision = authorityRevision
                      ProjectionCut =
                        { ProviderRunId = normalize providerRun
                          ToolCallId = normalize toolCall }
                      Outcome = retirementOutcome }

                Decision.retire state (RoadId.create road) (IncumbencyId.create incumbent) summary
                |> result

    let private phaseName phase =
        match phase with
        | IncumbencyPhase.AuditPending -> "AuditPending"
        | IncumbencyPhase.WorkOwned -> "WorkOwned"
        | IncumbencyPhase.PerfectAwaitingRetirement -> "PerfectAwaitingRetirement"
        | IncumbencyPhase.RetirementCleanupBlocked -> "RetirementCleanupBlocked"

    let private nullableString value =
        match value with
        | None -> null
        | Some text -> box text

    let view state road =
        match Fold.view state (RoadId.create road) with
        | None -> null
        | Some roadView ->
            box
                {| activeIncumbency = roadView.ActiveIncumbency |> Option.map IncumbencyId.value |> nullableString
                   iterationOrdinal = roadView.IterationOrdinal
                   phase = roadView.ActivePhase |> Option.map phaseName |> nullableString
                   retirementConfirmed = Option.isSome roadView.RetirementConfirmation
                   retired = roadView.RetiredIncumbencies |> List.map IncumbencyId.value |> List.toArray |}

    let authority state road =
        match Fold.view state (RoadId.create road) with
        | None -> null
        | Some roadView ->
            box
                {| roadRevision = AuthorityRevision.value roadView.AuthorityRevision
                   revisionHistory = roadView.AuthorityRevisions |> List.map AuthorityRevision.value |> List.toArray
                   activeRevision =
                    roadView.ActiveAuthorityRevision
                    |> Option.map AuthorityRevision.value
                    |> nullableString
                   messageIds =
                    roadView.AuthorityMessageIds
                    |> List.map PhysicalUserMessageId.value
                    |> List.toArray |}

    let private outcomeName outcome =
        match outcome with
        | RetirementOutcome.Relayed -> "Relayed"
        | RetirementOutcome.Delivered -> "Delivered"

    let retirement state road =
        match Fold.view state (RoadId.create road) with
        | Some roadView ->
            match roadView.LatestRetirement with
            | Some retirement ->
                box
                    {| retirementId = RetirementId.value retirement.Id
                       incumbentId = IncumbencyId.value retirement.IncumbencyId
                       outcome = outcomeName retirement.Outcome
                       providerRunId = retirement.ProjectionCut.ProviderRunId
                       toolCallId = retirement.ProjectionCut.ToolCallId
                       authorityRevision = AuthorityRevision.value retirement.AuthorityRevision |}
            | None -> null
        | None -> null

    let roadDevOps state road =
        match Fold.view state (RoadId.create road) with
        | None -> null
        | Some roadView ->
            let devopsId = roadView.BoundDevOps |> nullableString

            let incumbentId =
                roadView.ActiveIncumbency |> Option.map IncumbencyId.value |> nullableString

            box
                {| devopsId = devopsId
                   incumbentId = incumbentId
                   modelTarget = roadView.BoundDevOpsModelTarget |> nullableString |}

    let bindRoadDevOps state road devopsId (modelTarget: obj) =
        let target =
            if isNull modelTarget then
                None
            elif emitJsExpr modelTarget "typeof $0 === 'string' && $0.trim().length > 0" then
                Some(string modelTarget)
            else
                None

        Decision.bindRoadDevOps state (RoadId.create road) devopsId target |> result
