namespace Wanxiangshu.Mission.Relay

open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation.Identity

type private AssessmentRecord =
    { Id: AssessmentId
      Binding: AssessmentBinding
      SnapshotId: WorkspaceSnapshotId
      AuthorityRevision: AuthorityRevision
      Scores: ScoreVector }

type private ActiveIncumbency =
    { Id: IncumbencyId
      SnapshotId: WorkspaceSnapshotId
      AuthorityRevision: AuthorityRevision
      Phase: IncumbencyPhase
      Assessment: AssessmentRecord option }

type private RoadState =
    { AuthorityRevision: AuthorityRevision
      AuthorityRevisions: AuthorityRevision list
      AuthorityMessageIds: PhysicalUserMessageId list
      Active: ActiveIncumbency option
      Retired: IncumbencyId list
      RetiredProviderRunIds: Set<string>
      SeenAssessmentIds: Set<string>
      Certificate: QualityCertificate option
      LatestRetirement: RetirementSummary option }

type RelayState = private RelayState of Map<string, RoadState>

type RoadView =
    { AuthorityRevision: AuthorityRevision
      AuthorityRevisions: AuthorityRevision list
      AuthorityMessageIds: PhysicalUserMessageId list
      ActiveIncumbency: IncumbencyId option
      ActivePhase: IncumbencyPhase option
      ActiveSnapshotId: WorkspaceSnapshotId option
      ActiveAuthorityRevision: AuthorityRevision option
      AcceptedAssessmentTransport: (string * string) option
      RetiredIncumbencies: IncumbencyId list
      RetiredProviderRunIds: Set<string>
      Certificate: QualityCertificate option
      LatestRetirement: RetirementSummary option }

module private Internal =
    let private key roadId = RoadId.value roadId
    let road roadId (RelayState roads) = Map.tryFind (key roadId) roads

    let update roadId roadState (RelayState roads) =
        RelayState(Map.add (key roadId) roadState roads)

    let certificateId assessmentId =
        QualityCertificateId.create ("certificate:" + AssessmentId.value assessmentId)

    let require error =
        function
        | Some value -> Ok value
        | None -> Error error

    let private newCertificate perfect assessmentId (active: ActiveIncumbency) snapshotId authorityRevision binding =
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
        if perfect then
            IncumbencyPhase.PerfectAwaitingRetirement
        else
            IncumbencyPhase.WorkOwned

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

        let certificate =
            newCertificate perfect assessmentId active snapshotId authorityRevision binding

        let updatedActive =
            { active with
                Assessment =
                    Some
                        { Id = assessmentId
                          Binding = binding
                          SnapshotId = snapshotId
                          AuthorityRevision = authorityRevision
                          Scores = scores }
                Phase = phaseAfterAssessment perfect }

        { current with
            Active = Some updatedActive
            SeenAssessmentIds = Set.add (AssessmentId.value assessmentId) current.SeenAssessmentIds
            Certificate = certificate }
        |> fun updated -> update roadId updated state
        |> Ok

    let private requireMatchingIncumbency (active: ActiveIncumbency) incumbencyId =
        if active.Id <> incumbencyId then
            Error "IncumbencyNotActive"
        else
            Ok()

    let private isExactAssessment
        (accepted: AssessmentRecord)
        assessmentId
        binding
        snapshotId
        authorityRevision
        scores
        =
        accepted.Id = assessmentId
        && accepted.Binding = binding
        && accepted.SnapshotId = snapshotId
        && accepted.AuthorityRevision = authorityRevision
        && accepted.Scores = scores

    let private isConflictingAssessment
        (accepted: AssessmentRecord)
        (assessmentId: AssessmentId)
        (binding: AssessmentBinding)
        =
        accepted.Id = assessmentId || accepted.Binding.ToolCallId = binding.ToolCallId

    let private decideStoredAssessment
        (accepted: AssessmentRecord)
        state
        assessmentId
        binding
        snapshotId
        authorityRevision
        scores
        =
        if isExactAssessment accepted assessmentId binding snapshotId authorityRevision scores then
            Ok state
        elif isConflictingAssessment accepted assessmentId binding then
            Error "AssessmentReplayConflict"
        else
            Error "AssessmentAlreadySubmitted"

    let private validateFreshAssessment
        (active: ActiveIncumbency)
        (current: RoadState)
        snapshotId
        authorityRevision
        assessmentId
        =
        if active.Phase <> IncumbencyPhase.AuditPending then
            Error "AssessmentNotAllowedInCurrentPhase"
        elif active.SnapshotId <> snapshotId then
            Error "AuditSnapshotStale"
        elif active.AuthorityRevision <> authorityRevision then
            Error "AuthorityRevisionStale"
        elif Set.contains (AssessmentId.value assessmentId) current.SeenAssessmentIds then
            Error "AssessmentReplayConflict"
        else
            Ok()

    let private commitFreshAssessment
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
        result {
            do! validateFreshAssessment active current snapshotId authorityRevision assessmentId

            return!
                acceptAssessment roadId state current active assessmentId binding snapshotId authorityRevision scores
        }

    let private decideAssessment
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
        | Some accepted ->
            decideStoredAssessment accepted state assessmentId binding snapshotId authorityRevision scores
        | None ->
            commitFreshAssessment roadId state current active assessmentId binding snapshotId authorityRevision scores

    let private assess
        roadId
        state
        (current: RoadState)
        (active: ActiveIncumbency)
        incumbencyId
        assessmentId
        binding
        snapshotId
        authorityRevision
        scores
        =
        result {
            do! requireMatchingIncumbency active incumbencyId

            return!
                decideAssessment roadId state current active assessmentId binding snapshotId authorityRevision scores
        }

    let private authorityReplay exactReplay state =
        if exactReplay then
            Ok state
        else
            Error "AuthorityRevisionReplayConflict"

    let private invalidateForAuthorityRevision next (certificate: QualityCertificate) =
        if certificate.Valid then
            { certificate with
                Valid = false
                InvalidationReason = Some("AuthorityRevisionAdvanced:" + AuthorityRevision.value next) }
        else
            certificate

    let private isExactAuthorityReplay
        (current: RoadState)
        (active: ActiveIncumbency)
        next
        snapshotId
        authorityMessageId
        =
        active.AuthorityRevision = next
        && active.SnapshotId = snapshotId
        && current.AuthorityRevisions |> List.tryLast = Some next
        && current.AuthorityMessageIds |> List.tryLast = Some authorityMessageId

    let private findAuthorityAdvanceViolation
        (current: RoadState)
        (active: ActiveIncumbency)
        expected
        next
        authorityMessageId
        =
        if List.contains next current.AuthorityRevisions then
            Some "AuthorityRevisionAlreadySuperseded"
        elif current.AuthorityRevision <> expected || active.AuthorityRevision <> expected then
            Some "AuthorityRevisionStale"
        elif next = expected then
            Some "AuthorityRevisionUnchanged"
        elif List.contains authorityMessageId current.AuthorityMessageIds then
            Some "AuthorityMessageAlreadyUsed"
        else
            None

    let private commitAuthorityAdvance
        roadId
        state
        (current: RoadState)
        (active: ActiveIncumbency)
        next
        snapshotId
        authorityMessageId
        =
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

    let private decideFreshAuthorityAdvance
        roadId
        state
        (current: RoadState)
        (active: ActiveIncumbency)
        expected
        next
        authorityMessageId
        snapshotId
        =
        match findAuthorityAdvanceViolation current active expected next authorityMessageId with
        | Some error -> Error error
        | None -> commitAuthorityAdvance roadId state current active next snapshotId authorityMessageId

    let private decideAuthorityAdvance
        roadId
        state
        (current: RoadState)
        (active: ActiveIncumbency)
        expected
        next
        authorityMessageId
        snapshotId
        =
        if current.AuthorityRevision = next then
            authorityReplay (isExactAuthorityReplay current active next snapshotId authorityMessageId) state
        else
            decideFreshAuthorityAdvance roadId state current active expected next authorityMessageId snapshotId

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
        result {
            do! requireMatchingIncumbency active incumbentId
            return! decideAuthorityAdvance roadId state current active expected next authorityMessageId snapshotId
        }

    let private requireRetirementAssessment (active: ActiveIncumbency) =
        match active.Assessment with
        | None -> Error "AssessmentRequired"
        | Some assessment -> Ok assessment

    let private checkRetirementAuthority
        (current: RoadState)
        (active: ActiveIncumbency)
        (retirement: RetirementSummary)
        =
        if
            retirement.AuthorityRevision <> current.AuthorityRevision
            || retirement.AuthorityRevision <> active.AuthorityRevision
        then
            Error "AuthorityRevisionMismatch"
        else
            Ok()

    let private checkAcceptedPrerequisites
        (active: ActiveIncumbency)
        (assessment: AssessmentRecord)
        (retirement: RetirementSummary)
        =
        if
            active.Phase <> IncumbencyPhase.PerfectAwaitingRetirement
            && active.Phase <> IncumbencyPhase.RetirementCleanupBlocked
        then
            Error "RetirementRequiresPerfectAssessment"
        elif retirement.SnapshotId <> assessment.SnapshotId then
            Error "RetirementSnapshotStale"
        else
            Ok()

    let private checkAcceptedCertificateBindings
        (certificate: QualityCertificate)
        (certificateId: QualityCertificateId)
        (active: ActiveIncumbency)
        (assessment: AssessmentRecord)
        (retirement: RetirementSummary)
        =
        if certificate.Id <> certificateId then
            Error "QualityCertificateMismatch"
        elif not certificate.Valid then
            Error "QualityCertificateInvalid"
        elif certificate.IncumbencyId <> active.Id then
            Error "QualityCertificateIncumbencyMismatch"
        elif certificate.SnapshotId <> assessment.SnapshotId then
            Error "QualityCertificateSnapshotMismatch"
        elif certificate.SnapshotId <> retirement.SnapshotId then
            Error "QualityCertificateSnapshotMismatch"
        elif certificate.AuthorityRevision <> retirement.AuthorityRevision then
            Error "QualityCertificateAuthorityMismatch"
        elif certificate.AuthorityRevision <> active.AuthorityRevision then
            Error "QualityCertificateAuthorityMismatch"
        else
            Ok()

    let private decideAcceptedCertificate
        (current: RoadState)
        (certificateId: QualityCertificateId)
        (active: ActiveIncumbency)
        (assessment: AssessmentRecord)
        (retirement: RetirementSummary)
        =
        match current.Certificate with
        | None -> Error "QualityCertificateNotFound"
        | Some certificate -> checkAcceptedCertificateBindings certificate certificateId active assessment retirement

    let private admitAcceptedOutcome
        (current: RoadState)
        (active: ActiveIncumbency)
        (assessment: AssessmentRecord)
        (certificateId: QualityCertificateId)
        (retirement: RetirementSummary)
        =
        result {
            do! checkAcceptedPrerequisites active assessment retirement
            return! decideAcceptedCertificate current certificateId active assessment retirement
        }

    let private admitContinueOutcome (current: RoadState) (active: ActiveIncumbency) =
        match current.Certificate with
        | Some certificate when certificate.Valid && certificate.IncumbencyId = active.Id ->
            Error "ValidCertificateRemains"
        | _ -> Ok()

    let private admitOutcomeForAssessment
        (current: RoadState)
        (active: ActiveIncumbency)
        (assessment: AssessmentRecord)
        (retirement: RetirementSummary)
        =
        match retirement.Outcome with
        | RetirementOutcome.Accepted certificateId ->
            admitAcceptedOutcome current active assessment certificateId retirement
        | RetirementOutcome.Continue -> admitContinueOutcome current active

    let private admitOutcome (current: RoadState) (active: ActiveIncumbency) (retirement: RetirementSummary) =
        result {
            let! assessment = requireRetirementAssessment active
            do! checkRetirementAuthority current active retirement
            return! admitOutcomeForAssessment current active assessment retirement
        }

    let private replayedRetirement (current: RoadState) state (retirement: RetirementSummary) =
        match current.Active, current.LatestRetirement with
        | None, Some accepted when accepted = retirement -> Some(Ok state)
        | None, Some accepted when accepted.Id = retirement.Id -> Some(Error "RetirementReplayConflict")
        | _ -> None

    let private requireRetirementTarget (current: RoadState) (retirement: RetirementSummary) =
        match current.Active with
        | None -> Error "NoActiveIncumbency"
        | Some active when active.Id <> retirement.IncumbencyId -> Error "IncumbencyNotActive"
        | Some _ when List.contains retirement.IncumbencyId current.Retired -> Error "IncumbencyAlreadyRetired"
        | Some active -> Ok active

    let private commitRetirement roadId state (current: RoadState) (retirement: RetirementSummary) =
        { current with
            Active = None
            Retired = current.Retired @ [ retirement.IncumbencyId ]
            RetiredProviderRunIds = Set.add retirement.ProjectionCut.ProviderRunId current.RetiredProviderRunIds
            LatestRetirement = Some retirement }
        |> fun updated -> update roadId updated state
        |> Ok

    let private commitNewRetirement roadId state (current: RoadState) (retirement: RetirementSummary) =
        result {
            let! active = requireRetirementTarget current retirement
            do! admitOutcome current active retirement
            return! commitRetirement roadId state current retirement
        }

    let private retire roadId state (current: RoadState) (retirement: RetirementSummary) =
        replayedRetirement current state retirement
        |> Option.defaultWith (fun () -> commitNewRetirement roadId state current retirement)

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
              RetiredProviderRunIds = Set.empty
              SeenAssessmentIds = Set.empty
              Certificate = None
              LatestRetirement = None }
            |> fun opened -> update roadId opened state
            |> Ok

    let private makePendingIncumbency (current: RoadState) incumbentId snapshotId =
        { Id = incumbentId
          SnapshotId = snapshotId
          AuthorityRevision = current.AuthorityRevision
          Phase = IncumbencyPhase.AuditPending
          Assessment = None }

    let private commitPendingIncumbency roadId state (current: RoadState) incumbentId snapshotId =
        update
            roadId
            { current with
                Active = Some(makePendingIncumbency current incumbentId snapshotId) }
            state
        |> Ok

    let private decideAcceptedReopen roadId state (current: RoadState) incumbentId snapshotId certificateId =
        match current.Certificate with
        | Some certificate when certificate.Id = certificateId && not certificate.Valid ->
            commitPendingIncumbency roadId state current incumbentId snapshotId
        | _ -> Error "RoadAlreadyAccepted"

    let private decideRetirementReopen roadId state (current: RoadState) incumbentId snapshotId retirement =
        match retirement.Outcome with
        | RetirementOutcome.Continue -> commitPendingIncumbency roadId state current incumbentId snapshotId
        | RetirementOutcome.Accepted certificateId ->
            decideAcceptedReopen roadId state current incumbentId snapshotId certificateId

    let private decideInactiveReopen roadId state (current: RoadState) incumbentId snapshotId =
        match current.LatestRetirement with
        | None -> commitPendingIncumbency roadId state current incumbentId snapshotId
        | Some retirement -> decideRetirementReopen roadId state current incumbentId snapshotId retirement

    let private decideIncumbencyOpen roadId state (current: RoadState) incumbentId snapshotId =
        match current.Active with
        | Some active when active.Id = incumbentId && active.SnapshotId = snapshotId -> Ok state
        | Some _ -> Error "ActiveIncumbencyAlreadyExists"
        | None when List.contains incumbentId current.Retired -> Error "RetiredIncumbencyCannotReactivate"
        | None -> decideInactiveReopen roadId state current incumbentId snapshotId

    let private openIncumbency roadId state incumbentId snapshotId =
        result {
            let! current = road roadId state |> require "RoadNotOpen"
            return! decideIncumbencyOpen roadId state current incumbentId snapshotId
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

                    update
                        roadId
                        { current with
                            Certificate = Some invalidated }
                        state
                    |> Ok
                | Some certificate when certificate.Id = certificateId -> Ok state
                | _ -> Error "QualityCertificateNotFound"
        }

    let private requireBlockTarget (current: RoadState) incumbencyId =
        match current.Active with
        | Some active when active.Id = incumbencyId -> Ok active
        | Some _ -> Error "IncumbencyNotActive"
        | None -> Error "NoActiveIncumbency"

    let private requireBlockAssessment (active: ActiveIncumbency) =
        match active.Assessment with
        | None -> Error "AssessmentRequired"
        | Some _ -> Ok()

    let private commitBlockedCleanup roadId state (current: RoadState) (active: ActiveIncumbency) =
        update
            roadId
            { current with
                Active =
                    Some
                        { active with
                            Phase = IncumbencyPhase.RetirementCleanupBlocked } }
            state
        |> Ok

    let private blockRetirementCleanup roadId state incumbencyId =
        result {
            let! current = road roadId state |> require "RoadNotOpen"
            let! active = requireBlockTarget current incumbencyId
            do! requireBlockAssessment active
            return! commitBlockedCleanup roadId state current active
        }

    let applyEvent roadId state event =
        match event with
        | RelayEvent.RoadOpened(eventRoadId, authorityRevision, authorityMessageId) ->
            openRoad roadId state eventRoadId authorityRevision authorityMessageId
        | RelayEvent.IncumbencyOpened(incumbentId, snapshotId) -> openIncumbency roadId state incumbentId snapshotId
        | RelayEvent.AssessmentCommitted(assessmentId, incumbencyId, binding, snapshotId, authorityRevision, scores) ->
            result {
                let! current = road roadId state |> require "RoadNotOpen"
                let! active = current.Active |> require "NoActiveIncumbency"

                return!
                    assess
                        roadId
                        state
                        current
                        active
                        incumbencyId
                        assessmentId
                        binding
                        snapshotId
                        authorityRevision
                        scores
            }
        | RelayEvent.AuthorityRevisionAdvanced(incumbentId, expected, next, authorityMessageId, snapshotId) ->
            result {
                let! current = road roadId state |> require "RoadNotOpen"
                let! active = current.Active |> require "NoActiveIncumbency"

                return!
                    advanceAuthority roadId state current active incumbentId expected next authorityMessageId snapshotId
            }
        | RelayEvent.QualityCertificateInvalidated(certificateId, reason) ->
            invalidateCertificate roadId state certificateId reason
        | RelayEvent.RetirementCleanupBlocked(incumbencyId, _) -> blockRetirementCleanup roadId state incumbencyId
        | RelayEvent.RetirementCommitted retirement ->
            result {
                let! current = road roadId state |> require "RoadNotOpen"
                return! retire roadId state current retirement
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
              ActiveSnapshotId = road.Active |> Option.map (fun active -> active.SnapshotId)
              ActiveAuthorityRevision = road.Active |> Option.map (fun active -> active.AuthorityRevision)
              AcceptedAssessmentTransport =
                road.Active
                |> Option.bind (fun active ->
                    active.Assessment
                    |> Option.map (fun assessment -> assessment.Binding.ToolCallId, assessment.Binding.PayloadDigest))
              RetiredIncumbencies = road.Retired
              RetiredProviderRunIds = road.RetiredProviderRunIds
              Certificate = road.Certificate
              LatestRetirement = road.LatestRetirement })

module Decision =
    let private commit state roadId events =
        match RelayTransaction.create events with
        | Error error -> Error error
        | Ok transaction -> Fold.apply state roadId transaction

    let openIncumbency state roadId incumbentId snapshotId authorityRevision =
        match Fold.view state roadId with
        | None ->
            let authorityMessageId =
                PhysicalUserMessageId.create (AuthorityRevision.value authorityRevision)

            commit
                state
                roadId
                [ RelayEvent.RoadOpened(roadId, authorityRevision, authorityMessageId)
                  RelayEvent.IncumbencyOpened(incumbentId, snapshotId) ]
        | Some view when view.AuthorityRevision <> authorityRevision -> Error "AuthorityRevisionMismatch"
        | Some _ -> commit state roadId [ RelayEvent.IncumbencyOpened(incumbentId, snapshotId) ]

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
                      incumbentId,
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

    let blockCleanup state roadId incumbentId blockerDigest =
        commit state roadId [ RelayEvent.RetirementCleanupBlocked(incumbentId, blockerDigest) ]

    let retire state roadId incumbentId retirement =
        match Fold.view state roadId with
        | None -> Error "RoadNotOpen"
        | Some view when view.ActiveIncumbency = Some incumbentId ->
            commit state roadId [ RelayEvent.RetirementCommitted retirement ]
        | Some _ -> Error "IncumbencyNotActive"
