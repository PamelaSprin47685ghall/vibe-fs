namespace Wanxiangshu.Mission.Relay

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type RelayEvent =
    | RoadOpened of RoadId * AuthorityRevision * PhysicalUserMessageId
    | IncumbencyOpened of IncumbencyId * WorkspaceSnapshotId * BatonSource
    | AssessmentCommitted of AssessmentId * AssessmentBinding * WorkspaceSnapshotId * AuthorityRevision * ScoreVector
    | AuthorityRevisionAdvanced of
        IncumbencyId *
        expected: AuthorityRevision *
        next: AuthorityRevision *
        PhysicalUserMessageId *
        WorkspaceSnapshotId
    | QualityCertificateInvalidated of QualityCertificateId * reason: string
    | RetirementCleanupBlocked of IncumbencyId * blockerDigest: string
    | ExitRequiredNudgeScheduled of IncumbencyId * causalFrontier: string
    | RetirementCommitted of RetirementSummary
    | SuccessorRequested of predecessor: RetirementId * reason: string
    | SuccessorActivated of predecessor: RetirementId * IncumbencyId * WorkspaceSnapshotId * AuthorityRevision

type RelayTransaction

module RelayTransaction =
    val create: RelayEvent list -> Result<RelayTransaction, string>
    val events: RelayTransaction -> RelayEvent list

[<RequireQualifiedAccess>]
type RelayFactCases =
    | TransactionCommitted of
        {| RoadId: RoadId
           Transaction: RelayTransaction |}
