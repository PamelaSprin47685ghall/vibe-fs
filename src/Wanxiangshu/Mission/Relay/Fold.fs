namespace Wanxiangshu.Mission.Relay

open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation.Identity

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
      AuthorityRevisions: AuthorityRevision list
      AuthorityMessageIds: PhysicalUserMessageId list
      Active: ActiveIncumbency option
      Retired: IncumbencyId list
      OpenObligations: ScoreDimension list
      ExitRequiredNudgeFrontiers: Set<string>
      Certificate: QualityCertificate option
      LatestRetirement: RetirementSummary option }

type RelayState = private RelayState of Map<string, RoadState>

type RoadView =
    { AuthorityRevision: AuthorityRevision
      AuthorityRevisions: AuthorityRevision list
      AuthorityMessageIds: PhysicalUserMessageId list
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

    let require error = function
        | Some value -> Ok value
        | None -> Error error

    let private newCertificate
        perfect
        assessmentId
        (active: ActiveIncumbency)
        snapshotId
        authorityRevision
        binding
        =
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

    let private phaseAfterAssessment perfect =
        if perfect then IncumbencyPhase.PerfectAwaitingRetirement else IncumbencyPhase.WorkOwned

    let private acceptAssessment
        roadId
        state
        (current: RoadState)
        (active: ActiveIncumbency)
        assessmentId
        binding
        snapshotId
        authorityRevision
        scores
        =
        let perfect = ScoreVector.allPerfect scores
        let certificate = newCertificate perfect assessmentId active snapshotId authorityRevision binding

        let updatedActive =
            { active with
                Assessment =
                    Some
                        { Id = assessmentId
                          Binding = binding
                          Scores = scores }
                Phase = phaseAfterAssessment perfect }

        { current with
            Active = Some updatedActive
            OpenObligations = ScoreVector.lowDimensions scores
            Certificate = certificate }
        |> fun updated -> update roadId updated state
        |> Ok

    let private assess
        roadId
        state
        (current: RoadState)
        (active: ActiveIncumbency)
        assessmentId
        binding
        snapshotId
        authorityRevision
        scores
        =
        match active.Assessment with
        | Some accepted
            when accepted.Binding.ToolCallId = binding.ToolCallId
                 && accepted.Binding.PayloadDigest = binding.PayloadDigest ->
            Ok state
        | Some accepted when accepted.Binding.ToolCallId = binding.ToolCallId -> Error "AssessmentReplayConflict"
        | Some _ -> Error "AssessmentAlreadySubmitted"
        | None when active.Phase <> IncumbencyPhase.AuditPending -> Error "AssessmentNotAllowedInCurrentPhase"
        | None when active.SnapshotId <> snapshotId -> Error "AuditSnapshotStale"
        | None when active.AuthorityRevision <> authorityRevision -> Error "AuthorityRevisionStale"
        | None -> acceptAssessment roadId state current active assessmentId binding snapshotId authorityRevision scores

    let private authorityReplay exactReplay state =
        if exactReplay then Ok state else Error "AuthorityRevisionReplayConflict"

    let private invalidateForAuthorityRevision next (certificate: QualityCertificate) =
        if certificate.Valid then
            { certificate with
                Valid = false
                InvalidationReason = Some("AuthorityRevisionAdvanced:" + AuthorityRevision.value next) }
        else
            certificate

    let private advanceAuthority
        roadId
        state
        (current: RoadState)
        (active: ActiveIncumbency)
        incumbentId
        expected
        next
        authorityMessageId
        snapshotId
        =
        let exactReplay =
            active.AuthorityRevision = next
            && active.SnapshotId = snapshotId
            && current.AuthorityRevisions |> List.tryLast = Some next
            && current.AuthorityMessageIds |> List.tryLast = Some authorityMessageId

        if active.Id <> incumbentId then
            Error "IncumbencyNotActive"
        elif current.AuthorityRevision = next then
            authorityReplay exactReplay state
        elif List.contains next current.AuthorityRevisions then
            Error "AuthorityRevisionAlreadySuperseded"
        elif current.AuthorityRevision <> expected || active.AuthorityRevision <> expected then
            Error "AuthorityRevisionStale"
        elif next = expected then
            Error "AuthorityRevisionUnchanged"
        elif List.contains authorityMessageId current.AuthorityMessageIds then
            Error "AuthorityMessageAlreadyUsed"
        else
            let updatedActive =
                { active with
                    AuthorityRevision = next
                    SnapshotId = snapshotId }

            { current with
                AuthorityRevision = next
                AuthorityRevisions = current.AuthorityRevisions @ [ next ]
                AuthorityMessageIds = current.AuthorityMessageIds @ [ authorityMessageId ]
                Active = Some updatedActive
                Certificate = current.Certificate |> Option.map (invalidateForAuthorityRevision next) }
            |> fun updated -> update roadId updated state
            |> Ok

    let private retire roadId state (current: RoadState) (retirement: RetirementSummary) =
        match current.Active, current.LatestRetirement with
        | None, Some accepted
            when accepted.Id = retirement.Id
                 && accepted.IncumbencyId = retirement.IncumbencyId ->
            Ok state
        | None, Some accepted when accepted.Id = retirement.Id -> Error "RetirementReplayConflict"
        | None, _ -> Error "NoActiveIncumbency"
        | Some active, _ when active.Id <> retirement.IncumbencyId -> Error "IncumbencyNotActive"
        | Some _, _ when List.contains retirement.IncumbencyId current.Retired -> Error "IncumbencyAlreadyRetired"
        | Some _, _ ->
            { current with
                Active = None
                Retired = current.Retired @ [ retirement.IncumbencyId ]
                LatestRetirement = Some retirement }
            |> fun updated -> update roadId updated state
            |> Ok

    let private activateSuccessor
        roadId
        state
        (current: RoadState)
        predecessor
        incumbentId
        snapshotId
        authorityRevision
        =
        match current.LatestRetirement, current.Active with
        | None, _ -> Error "PredecessorRetirementNotCommitted"
        | Some retirement, _ when retirement.Id <> predecessor -> Error "PredecessorRetirementNotCommitted"
        | Some retirement, _ when not retirement.SuccessorRequested -> Error "SuccessorNotRequested"
        | Some _, Some active
            when active.Id = incumbentId
                 && active.Source = BatonSource.Retirement predecessor
                 && active.SnapshotId = snapshotId
                 && active.AuthorityRevision = authorityRevision ->
            Ok state
        | Some _, Some _ -> Error "ActiveIncumbencyAlreadyExists"
        | Some _, None when current.Retired |> List.contains incumbentId -> Error "RetiredIncumbencyCannotReactivate"
        | Some _, None ->
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

    let private openRoad roadId state eventRoadId authorityRevision authorityMessageId =
        match eventRoadId = roadId, road roadId state with
        | false, _ -> Error "RoadIdentityMismatch"
        | true, Some _ -> Error "RoadAlreadyOpen"
        | true, None ->
            { AuthorityRevision = authorityRevision
              AuthorityRevisions = [ authorityRevision ]
              AuthorityMessageIds = [ authorityMessageId ]
              Active = None
              Retired = []
              OpenObligations = []
              ExitRequiredNudgeFrontiers = Set.empty
              Certificate = None
              LatestRetirement = None }
            |> fun opened -> update roadId opened state
            |> Ok

    let private openIncumbency roadId state incumbentId snapshotId source =
        result {
            let! current = road roadId state |> require "RoadNotOpen"

            return!
                match current.Active with
                | Some _ -> Error "ActiveIncumbencyAlreadyExists"
                | None when List.contains incumbentId current.Retired -> Error "RetiredIncumbencyCannotReactivate"
                | None ->
                    let active =
                        { Id = incumbentId
                          SnapshotId = snapshotId
                          AuthorityRevision = current.AuthorityRevision
                          Source = source
                          Phase = IncumbencyPhase.AuditPending
                          Assessment = None }

                    update roadId { current with Active = Some active } state |> Ok
        }

    let private invalidateCertificate roadId state certificateId reason =
        result {
            let! current = road roadId state |> require "RoadNotOpen"

            return!
                match current.Certificate with
                | Some certificate when certificate.Id = certificateId && certificate.Valid ->
                    let invalidated =
                        { certificate with
                            Valid = false
                            InvalidationReason = Some reason }

                    update roadId { current with Certificate = Some invalidated } state |> Ok
                | Some certificate when certificate.Id = certificateId -> Ok state
                | _ -> Error "QualityCertificateNotFound"
        }

    let private blockRetirementCleanup roadId state incumbencyId =
        result {
            let! current = road roadId state |> require "RoadNotOpen"

            return!
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
        }

    let private scheduleExitNudge roadId state incumbencyId causalFrontier =
        result {
            let! current = road roadId state |> require "RoadNotOpen"

            return!
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
        }

    let private requestSuccessor roadId state predecessor =
        result {
            let! current = road roadId state |> require "RoadNotOpen"

            return!
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
        }

    let applyEvent roadId state event =
        match event with
        | RelayEvent.RoadOpened(eventRoadId, authorityRevision, authorityMessageId) ->
            openRoad roadId state eventRoadId authorityRevision authorityMessageId
        | RelayEvent.IncumbencyOpened(incumbentId, snapshotId, source) ->
            openIncumbency roadId state incumbentId snapshotId source
        | RelayEvent.AssessmentCommitted(assessmentId, binding, snapshotId, authorityRevision, scores) ->
            result {
                let! current = road roadId state |> require "RoadNotOpen"
                let! active = current.Active |> require "NoActiveIncumbency"
                return! assess roadId state current active assessmentId binding snapshotId authorityRevision scores
            }
        | RelayEvent.AuthorityRevisionAdvanced(incumbentId, expected, next, authorityMessageId, snapshotId) ->
            result {
                let! current = road roadId state |> require "RoadNotOpen"
                let! active = current.Active |> require "NoActiveIncumbency"
                return! advanceAuthority roadId state current active incumbentId expected next authorityMessageId snapshotId
            }
        | RelayEvent.QualityCertificateInvalidated(certificateId, reason) ->
            invalidateCertificate roadId state certificateId reason
        | RelayEvent.RetirementCleanupBlocked(incumbencyId, _) ->
            blockRetirementCleanup roadId state incumbencyId
        | RelayEvent.ExitRequiredNudgeScheduled(incumbencyId, causalFrontier) ->
            scheduleExitNudge roadId state incumbencyId causalFrontier
        | RelayEvent.RetirementCommitted retirement ->
            result {
                let! current = road roadId state |> require "RoadNotOpen"
                return! retire roadId state current retirement
            }
        | RelayEvent.SuccessorRequested(predecessor, _) ->
            requestSuccessor roadId state predecessor
        | RelayEvent.SuccessorActivated(predecessor, incumbentId, snapshotId, authorityRevision) ->
            result {
                let! current = road roadId state |> require "RoadNotOpen"
                return! activateSuccessor roadId state current predecessor incumbentId snapshotId authorityRevision
            }

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
              AuthorityRevisions = road.AuthorityRevisions
              AuthorityMessageIds = road.AuthorityMessageIds
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
            let authorityMessageId =
                PhysicalUserMessageId.create (AuthorityRevision.value authorityRevision)

            commit
                state
                roadId
                [ RelayEvent.RoadOpened(roadId, authorityRevision, authorityMessageId)
                  RelayEvent.IncumbencyOpened(incumbentId, snapshotId, source) ]
        | Some _ -> commit state roadId [ RelayEvent.IncumbencyOpened(incumbentId, snapshotId, source) ]

    let advanceAuthority state roadId incumbentId expected next authorityMessageId snapshotId =
        commit
            state
            roadId
            [ RelayEvent.AuthorityRevisionAdvanced(incumbentId, expected, next, authorityMessageId, snapshotId) ]

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
        result {
            let! view = Fold.view state roadId |> Internal.require "RoadNotOpen"
            let! certificate = view.Certificate |> Internal.require "QualityCertificateNotFound"
            return! commit state roadId [ RelayEvent.QualityCertificateInvalidated(certificate.Id, reason) ]
        }

    let retire state roadId incumbentId retirement =
        match Fold.view state roadId with
        | None -> Error "RoadNotOpen"
        | Some view when view.ActiveIncumbency = Some incumbentId ->
            commit state roadId [ RelayEvent.RetirementCommitted retirement ]
        | Some _ -> Error "IncumbencyNotActive"

    let activateSuccessor state roadId predecessor incumbentId snapshotId authorityRevision =
        commit state roadId [ RelayEvent.SuccessorActivated(predecessor, incumbentId, snapshotId, authorityRevision) ]

