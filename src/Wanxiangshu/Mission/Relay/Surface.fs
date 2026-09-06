namespace Wanxiangshu.Mission.Relay

open Wanxiangshu.Foundation.Identity

module Surface =
    let empty () = Fold.empty

    let private result value =
        match value with
        | Ok state -> box {| ok = true; state = state |}
        | Error error -> box {| ok = false; error = error |}

    let openIncumbency state road incumbent snapshot authority =
        Decision.openIncumbency
            state
            (RoadId.create road)
            (IncumbencyId.create incumbent)
            (WorkspaceSnapshotId.create snapshot)
            (AuthorityRevision.create authority)
        |> result

    let assess
        state
        road
        incumbent
        assessment
        snapshot
        authority
        languageAlgorithms
        simplicity
        structure
        granularity
        testsEvidence
        logicReliabilityBoundaries
        callerErgonomics
        completeness
        =
        match
            ScoreVector.tryCreateStrings
                [ languageAlgorithms
                  simplicity
                  structure
                  granularity
                  testsEvidence
                  logicReliabilityBoundaries
                  callerErgonomics
                  completeness ]
        with
        | Error error -> box {| ok = false; error = error |}
        | Ok scores ->
            let payloadDigest =
                scores |> ScoreVector.values |> List.map ScoreGrade.format |> String.concat ","

            let binding =
                { PhysicalUserMessageId = authority
                  ProviderRunId = "surface-run"
                  ToolCallId = assessment
                  NarrativeDigest = "surface-narrative"
                  PayloadDigest = payloadDigest
                  RootRequestDigest = "surface-root"
                  RequirementSetDigest = "surface-requirements"
                  EvidenceFrontierDigest = "surface-evidence" }

            Decision.assess
                state
                (RoadId.create road)
                (IncumbencyId.create incumbent)
                (AssessmentId.create assessment)
                binding
                (WorkspaceSnapshotId.create snapshot)
                (AuthorityRevision.create authority)
                scores
            |> result

    let invalidateCertificate state road reason =
        Decision.invalidateCertificate state (RoadId.create road) reason |> result

    let advanceAuthority state road incumbent expected next authorityMessageId snapshot =
        Decision.advanceAuthority
            state
            (RoadId.create road)
            (IncumbencyId.create incumbent)
            (AuthorityRevision.create expected)
            (AuthorityRevision.create next)
            (PhysicalUserMessageId.create authorityMessageId)
            (WorkspaceSnapshotId.create snapshot)
        |> result

    let blockCleanup state road incumbent blockerDigest =
        Decision.blockCleanup state (RoadId.create road) (IncumbencyId.create incumbent) blockerDigest
        |> result

    let private normalize (value: string) = if isNull value then "" else value

    let private activeRetirementContext state road incumbent =
        match Fold.view state (RoadId.create road) with
        | None -> Error "RoadNotOpen"
        | Some view ->
            match view.ActiveIncumbency, view.ActiveAuthorityRevision with
            | Some activeId, Some authorityRevision when activeId = IncumbencyId.create incumbent ->
                Ok authorityRevision
            | _ -> Error "IncumbencyNotActive"

    let retireContinue state road incumbent retirement providerRun toolCall snapshot =
        match normalize snapshot with
        | "" ->
            box
                {| ok = false
                   error = "RetirementSnapshotStale" |}
        | snapshotValue ->
            match activeRetirementContext state road incumbent with
            | Error error -> box {| ok = false; error = error |}
            | Ok authorityRevision ->
                let summary =
                    { Id = RetirementId.create retirement
                      IncumbencyId = IncumbencyId.create incumbent
                      SnapshotId = WorkspaceSnapshotId.create snapshotValue
                      AuthorityRevision = authorityRevision
                      ProjectionCut =
                        { ProviderRunId = normalize providerRun
                          ToolCallId = normalize toolCall }
                      Outcome = RetirementOutcome.Continue }

                Decision.retire state (RoadId.create road) (IncumbencyId.create incumbent) summary
                |> result

    let retireAccepted state road incumbent retirement providerRun toolCall certificateId snapshot =
        match normalize certificateId, normalize snapshot with
        | "", _ ->
            box
                {| ok = false
                   error = "MissingQualityCertificate" |}
        | _, "" ->
            box
                {| ok = false
                   error = "RetirementSnapshotStale" |}
        | certificate, snapshotValue ->
            match activeRetirementContext state road incumbent with
            | Error error -> box {| ok = false; error = error |}
            | Ok authorityRevision ->
                let summary =
                    { Id = RetirementId.create retirement
                      IncumbencyId = IncumbencyId.create incumbent
                      SnapshotId = WorkspaceSnapshotId.create snapshotValue
                      AuthorityRevision = authorityRevision
                      ProjectionCut =
                        { ProviderRunId = normalize providerRun
                          ToolCallId = normalize toolCall }
                      Outcome = RetirementOutcome.Accepted(QualityCertificateId.create certificate) }

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
                   phase = roadView.ActivePhase |> Option.map phaseName |> nullableString
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
                   activeSnapshot =
                    roadView.ActiveSnapshotId
                    |> Option.map WorkspaceSnapshotId.value
                    |> nullableString
                   messageIds =
                    roadView.AuthorityMessageIds
                    |> List.map PhysicalUserMessageId.value
                    |> List.toArray |}

    let certificate state road =
        match Fold.view state (RoadId.create road) with
        | Some roadView ->
            match roadView.Certificate with
            | Some certificate ->
                box
                    {| assessmentId = AssessmentId.value certificate.AssessmentId
                       snapshotId = WorkspaceSnapshotId.value certificate.SnapshotId
                       authorityRevision = AuthorityRevision.value certificate.AuthorityRevision
                       valid = certificate.Valid |}
            | None -> null
        | None -> null

    let private outcomeName outcome =
        match outcome with
        | RetirementOutcome.Continue -> "Continue"
        | RetirementOutcome.Accepted _ -> "Accepted"

    let private outcomeCertificate outcome =
        match outcome with
        | RetirementOutcome.Continue -> null
        | RetirementOutcome.Accepted certificateId -> box (QualityCertificateId.value certificateId)

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
                       certificateId = outcomeCertificate retirement.Outcome
                       snapshotId = WorkspaceSnapshotId.value retirement.SnapshotId
                       authorityRevision = AuthorityRevision.value retirement.AuthorityRevision |}
            | None -> null
        | None -> null
