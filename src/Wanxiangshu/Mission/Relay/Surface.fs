namespace Wanxiangshu.Mission.Relay

open Wanxiangshu.Foundation.Identity

module Surface =
    let empty () = Fold.empty

    let private result value =
        match value with
        | Ok state -> box {| ok = true; state = state |}
        | Error error -> box {| ok = false; error = error |}

    let private source value =
        match value with
        | "ExistingWorld" -> Ok BatonSource.ExistingWorld
        | "Retirement" -> Ok(BatonSource.Retirement(RetirementId.create "external-predecessor"))
        | _ -> Error "UnknownBatonSource"

    let openIncumbency state road incumbent snapshot authority sourceName =
        match source sourceName with
        | Error error -> box {| ok = false; error = error |}
        | Ok batonSource ->
            Decision.openIncumbency
                state
                (RoadId.create road)
                (IncumbencyId.create incumbent)
                (WorkspaceSnapshotId.create snapshot)
                (AuthorityRevision.create authority)
                batonSource
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
            ScoreVector.tryCreate
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
                scores |> ScoreVector.values |> List.map string |> String.concat ","

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

    let retire state road incumbent retirement snapshot baton cut qualityCandidateAccepted =
        let envelope =
            { SchemaVersion = 1
              RoadId = road
              FromIncumbencyId = incumbent
              AuthorityRevision = "surface-authority"
              SnapshotId = snapshot
              OpenObligations = []
              EvidenceRefs = [] }

        let projectionCut =
            { RetiredIncumbencyId = incumbent
              ThroughProviderRunId = "surface-run"
              ThroughToolCallId = "surface-tool"
              StaleProviderRunIds = [ "surface-run" ] }

        let summary =
            { Id = RetirementId.create retirement
              IncumbencyId = IncumbencyId.create incumbent
              SnapshotId = WorkspaceSnapshotId.create snapshot
              BatonId = BatonId.create baton
              Baton = envelope
              ProjectionCutId = ProjectionCutId.create cut
              ProjectionCut = projectionCut
              SuccessorRequested = not qualityCandidateAccepted
              QualityCandidateAccepted = qualityCandidateAccepted }

        Decision.retire state (RoadId.create road) (IncumbencyId.create incumbent) summary
        |> result

    let activateSuccessor state road predecessor incumbent snapshot authority =
        Decision.activateSuccessor
            state
            (RoadId.create road)
            (RetirementId.create predecessor)
            (IncumbencyId.create incumbent)
            (WorkspaceSnapshotId.create snapshot)
            (AuthorityRevision.create authority)
        |> result

    let private phaseName phase =
        match phase with
        | IncumbencyPhase.AuditPending -> "AuditPending"
        | IncumbencyPhase.WorkOwned -> "WorkOwned"
        | IncumbencyPhase.PerfectAwaitingRetirement -> "PerfectAwaitingRetirement"
        | IncumbencyPhase.RetirementCleanupBlocked -> "RetirementCleanupBlocked"

    let private sourceName source =
        match source with
        | BatonSource.ExistingWorld -> "ExistingWorld"
        | BatonSource.Retirement _ -> "Retirement"

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
                   source = roadView.ActiveSource |> Option.map sourceName |> nullableString
                   retired = roadView.RetiredIncumbencies |> List.map IncumbencyId.value |> List.toArray |}

    let obligations state road =
        match Fold.view state (RoadId.create road) with
        | None -> [||]
        | Some roadView -> roadView.OpenObligations |> List.map ScoreDimension.fieldName |> List.toArray

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

    let retirement state road =
        match Fold.view state (RoadId.create road) with
        | Some roadView ->
            match roadView.LatestRetirement with
            | Some retirement ->
                box
                    {| retirementId = RetirementId.value retirement.Id
                       incumbentId = IncumbencyId.value retirement.IncumbencyId
                       snapshotId = WorkspaceSnapshotId.value retirement.SnapshotId
                       batonId = BatonId.value retirement.BatonId
                       projectionCutId = ProjectionCutId.value retirement.ProjectionCutId
                       successorRequested = retirement.SuccessorRequested
                       qualityCandidateAccepted = retirement.QualityCandidateAccepted |}
            | None -> null
        | None -> null
