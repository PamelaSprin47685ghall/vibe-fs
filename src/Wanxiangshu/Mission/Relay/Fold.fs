namespace Wanxiangshu.Mission.Relay

type private AssessmentRecord =
    { Id: AssessmentId
      Binding: AssessmentBinding
      Scores: ScoreVector }

type private ActiveIncumbency =
    { Id: IncumbencyId
      SnapshotId: WorkspaceSnapshotId
      AuthorityRevision: AuthorityRevision
      Source: BatonSource
      Phase: IncumbencyPhase
      Assessment: AssessmentRecord option }

type private RoadState =
    { AuthorityRevision: AuthorityRevision
      Active: ActiveIncumbency option
      Retired: IncumbencyId list
      OpenObligations: ScoreDimension list
      ExitRequiredNudgeFrontiers: Set<string>
      Certificate: QualityCertificate option
      LatestRetirement: RetirementSummary option }

type RelayState = private RelayState of Map<string, RoadState>

type RoadView =
    { AuthorityRevision: AuthorityRevision
      ActiveIncumbency: IncumbencyId option
      ActivePhase: IncumbencyPhase option
      ActiveSource: BatonSource option
      ActiveSnapshotId: WorkspaceSnapshotId option
      ActiveAuthorityRevision: AuthorityRevision option
      AcceptedAssessmentTransport: (string * string) option
      ExitRequiredNudgeFrontiers: Set<string>
      RetiredIncumbencies: IncumbencyId list
      OpenObligations: ScoreDimension list
      Certificate: QualityCertificate option
      LatestRetirement: RetirementSummary option }

module private Internal =
    let private key roadId = RoadId.value roadId
    let road roadId (RelayState roads) = Map.tryFind (key roadId) roads
    let update roadId roadState (RelayState roads) = RelayState(Map.add (key roadId) roadState roads)

    let certificateId assessmentId =
        QualityCertificateId.create ("certificate:" + AssessmentId.value assessmentId)

    let applyEvent roadId state event =
        match event with
        | RelayEvent.RoadOpened(eventRoadId, authorityRevision) ->
            if eventRoadId <> roadId then
                Error "RoadIdentityMismatch"
            else
                match road roadId state with
                | Some _ -> Error "RoadAlreadyOpen"
                | None ->
                    { AuthorityRevision = authorityRevision
                      Active = None
                      Retired = []
                      OpenObligations = []
                      ExitRequiredNudgeFrontiers = Set.empty
                      Certificate = None
                      LatestRetirement = None }
                    |> fun opened -> update roadId opened state
                    |> Ok
        | RelayEvent.IncumbencyOpened(incumbentId, snapshotId, source) ->
            match road roadId state with
            | None -> Error "RoadNotOpen"
            | Some current ->
                match current.Active with
                | Some _ -> Error "ActiveIncumbencyAlreadyExists"
                | None when List.contains incumbentId current.Retired ->
                    Error "RetiredIncumbencyCannotReactivate"
                | None ->
                    let active =
                        { Id = incumbentId
                          SnapshotId = snapshotId
                          AuthorityRevision = current.AuthorityRevision
                          Source = source
                          Phase = IncumbencyPhase.AuditPending
                          Assessment = None }

                    update roadId { current with Active = Some active } state |> Ok
        | RelayEvent.AssessmentCommitted(assessmentId, binding, snapshotId, authorityRevision, scores) ->
            match road roadId state with
            | None -> Error "RoadNotOpen"
            | Some current ->
                match current.Active with
                | None -> Error "NoActiveIncumbency"
                | Some active when active.Assessment |> Option.isSome ->
                    match active.Assessment with
                    | Some accepted
                        when accepted.Binding.ToolCallId = binding.ToolCallId
                             && accepted.Binding.PayloadDigest = binding.PayloadDigest ->
                        Ok state
                    | Some accepted when accepted.Binding.ToolCallId = binding.ToolCallId ->
                        Error "AssessmentReplayConflict"
                    | Some _ -> Error "AssessmentAlreadySubmitted"
                    | None -> Error "AssessmentAlreadySubmitted"
                | Some active when active.Phase <> IncumbencyPhase.AuditPending ->
                    Error "AssessmentNotAllowedInCurrentPhase"
                | Some active when active.SnapshotId <> snapshotId -> Error "AuditSnapshotStale"
                | Some active when active.AuthorityRevision <> authorityRevision ->
                    Error "AuthorityRevisionStale"
                | Some active ->
                    let perfect = ScoreVector.allPerfect scores

                    let certificate =
                        if perfect then
                            Some
                                { Id = certificateId assessmentId
                                  AssessmentId = assessmentId
                                  IncumbencyId = active.Id
                                  SnapshotId = snapshotId
                                  AuthorityRevision = authorityRevision
                                  Binding = binding
                                  Valid = true
                                  InvalidationReason = None }
                        else
                            None

                    let updatedActive =
                        { active with
                            Assessment =
                                Some
                                    { Id = assessmentId
                                      Binding = binding
                                      Scores = scores }
                            Phase =
                                if perfect then
                                    IncumbencyPhase.PerfectAwaitingRetirement
                                else
                                    IncumbencyPhase.WorkOwned }

                    { current with
                        Active = Some updatedActive
                        OpenObligations = ScoreVector.lowDimensions scores
                        Certificate = certificate }
                    |> fun updated -> update roadId updated state
                    |> Ok
        | RelayEvent.QualityCertificateInvalidated(certificateId, reason) ->
            match road roadId state with
            | None -> Error "RoadNotOpen"
            | Some current ->
                match current.Certificate with
                | Some certificate when certificate.Id = certificateId && certificate.Valid ->
                    let invalidated =
                        { certificate with
                            Valid = false
                            InvalidationReason = Some reason }

                    update roadId { current with Certificate = Some invalidated } state |> Ok
                | Some certificate when certificate.Id = certificateId -> Ok state
                | _ -> Error "QualityCertificateNotFound"
        | RelayEvent.RetirementCleanupBlocked(incumbencyId, _) ->
            match road roadId state with
            | None -> Error "RoadNotOpen"
            | Some current ->
                match current.Active with
                | Some active when active.Id = incumbencyId ->
                    update
                        roadId
                        { current with
                            Active = Some { active with Phase = IncumbencyPhase.RetirementCleanupBlocked } }
                        state
                    |> Ok
                | Some _ -> Error "IncumbencyNotActive"
                | None -> Error "NoActiveIncumbency"
        | RelayEvent.ExitRequiredNudgeScheduled(incumbencyId, causalFrontier) ->
            match road roadId state with
            | None -> Error "RoadNotOpen"
            | Some current ->
                match current.Active with
                | Some active when active.Id = incumbencyId ->
                    update
                        roadId
                        { current with
                            ExitRequiredNudgeFrontiers = Set.add causalFrontier current.ExitRequiredNudgeFrontiers }
                        state
                    |> Ok
                | Some _ -> Error "IncumbencyNotActive"
                | None -> Error "NoActiveIncumbency"
        | RelayEvent.RetirementCommitted retirement ->
            match road roadId state with
            | None -> Error "RoadNotOpen"
            | Some current ->
                match current.Active with
                | None ->
                    match current.LatestRetirement with
                    | Some accepted
                        when accepted.Id = retirement.Id
                             && accepted.IncumbencyId = retirement.IncumbencyId ->
                        Ok state
                    | Some accepted when accepted.Id = retirement.Id -> Error "RetirementReplayConflict"
                    | _ -> Error "NoActiveIncumbency"
                | Some active when active.Id <> retirement.IncumbencyId -> Error "IncumbencyNotActive"
                | Some _ when List.contains retirement.IncumbencyId current.Retired ->
                    Error "IncumbencyAlreadyRetired"
                | Some _ ->
                    { current with
                        Active = None
                        Retired = current.Retired @ [ retirement.IncumbencyId ]
                        LatestRetirement = Some retirement }
                    |> fun updated -> update roadId updated state
                    |> Ok
        | RelayEvent.SuccessorRequested(predecessor, _) ->
            match road roadId state with
            | None -> Error "RoadNotOpen"
            | Some current ->
                match current.LatestRetirement with
                | Some retirement when retirement.Id = predecessor && retirement.SuccessorRequested -> Ok state
                | Some retirement when retirement.Id = predecessor ->
                    update
                        roadId
                        { current with
                            LatestRetirement = Some { retirement with SuccessorRequested = true } }
                        state
                    |> Ok
                | _ -> Error "PredecessorRetirementNotCommitted"
        | RelayEvent.SuccessorActivated(predecessor, incumbentId, snapshotId, authorityRevision) ->
            match road roadId state with
            | None -> Error "RoadNotOpen"
            | Some current ->
                match current.LatestRetirement with
                | None -> Error "PredecessorRetirementNotCommitted"
                | Some retirement when retirement.Id <> predecessor ->
                    Error "PredecessorRetirementNotCommitted"
                | Some retirement when not retirement.SuccessorRequested -> Error "SuccessorNotRequested"
                | Some _ ->
                    match current.Active with
                    | Some active
                        when active.Id = incumbentId
                             && active.Source = BatonSource.Retirement predecessor
                             && active.SnapshotId = snapshotId
                             && active.AuthorityRevision = authorityRevision ->
                        Ok state
                    | Some _ -> Error "ActiveIncumbencyAlreadyExists"
                    | None when current.Retired |> List.contains incumbentId ->
                        Error "RetiredIncumbencyCannotReactivate"
                    | None ->
                        let active =
                            { Id = incumbentId
                              SnapshotId = snapshotId
                              AuthorityRevision = authorityRevision
                              Source = BatonSource.Retirement predecessor
                              Phase = IncumbencyPhase.AuditPending
                              Assessment = None }

                        { current with
                            AuthorityRevision = authorityRevision
                            Active = Some active
                            ExitRequiredNudgeFrontiers = Set.empty }
                        |> fun updated -> update roadId updated state
                        |> Ok

module Fold =
    let empty = RelayState Map.empty

    let apply state roadId transaction =
        RelayTransaction.events transaction
        |> List.fold
            (fun accumulated event ->
                match accumulated with
                | Error error -> Error error
                | Ok current -> Internal.applyEvent roadId current event)
            (Ok state)

    let view state roadId =
        Internal.road roadId state
        |> Option.map (fun (road: RoadState) ->
            { AuthorityRevision = road.AuthorityRevision
              ActiveIncumbency = road.Active |> Option.map (fun active -> active.Id)
              ActivePhase = road.Active |> Option.map (fun active -> active.Phase)
              ActiveSource = road.Active |> Option.map (fun active -> active.Source)
              ActiveSnapshotId = road.Active |> Option.map (fun active -> active.SnapshotId)
              ActiveAuthorityRevision = road.Active |> Option.map (fun active -> active.AuthorityRevision)
              AcceptedAssessmentTransport =
                road.Active
                |> Option.bind (fun active ->
                    active.Assessment
                    |> Option.map (fun assessment -> assessment.Binding.ToolCallId, assessment.Binding.PayloadDigest))
              ExitRequiredNudgeFrontiers = road.ExitRequiredNudgeFrontiers
              RetiredIncumbencies = road.Retired
              OpenObligations = road.OpenObligations
              Certificate = road.Certificate
              LatestRetirement = road.LatestRetirement })

module Decision =
    let private commit state roadId events =
        match RelayTransaction.create events with
        | Error error -> Error error
        | Ok transaction -> Fold.apply state roadId transaction

    let openIncumbency state roadId incumbentId snapshotId authorityRevision source =
        match Fold.view state roadId with
        | None ->
            commit
                state
                roadId
                [ RelayEvent.RoadOpened(roadId, authorityRevision)
                  RelayEvent.IncumbencyOpened(incumbentId, snapshotId, source) ]
        | Some _ -> commit state roadId [ RelayEvent.IncumbencyOpened(incumbentId, snapshotId, source) ]

    let assess state roadId incumbentId assessmentId binding snapshotId authorityRevision scores =
        match Fold.view state roadId with
        | None -> Error "RoadNotOpen"
        | Some view when view.ActiveIncumbency = Some incumbentId ->
            commit
                state
                roadId
                [ RelayEvent.AssessmentCommitted(
                      assessmentId,
                      binding,
                      snapshotId,
                      authorityRevision,
                      scores
                  ) ]
        | Some _ -> Error "IncumbencyNotActive"

    let invalidateCertificate state roadId reason =
        match Fold.view state roadId with
        | Some view ->
            match view.Certificate with
            | Some certificate ->
                commit state roadId [ RelayEvent.QualityCertificateInvalidated(certificate.Id, reason) ]
            | None -> Error "QualityCertificateNotFound"
        | None -> Error "RoadNotOpen"

    let retire state roadId incumbentId retirement =
        match Fold.view state roadId with
        | None -> Error "RoadNotOpen"
        | Some view when view.ActiveIncumbency = Some incumbentId ->
            commit state roadId [ RelayEvent.RetirementCommitted retirement ]
        | Some _ -> Error "IncumbencyNotActive"

    let activateSuccessor state roadId predecessor incumbentId snapshotId authorityRevision =
        commit state roadId [ RelayEvent.SuccessorActivated(predecessor, incumbentId, snapshotId, authorityRevision) ]

