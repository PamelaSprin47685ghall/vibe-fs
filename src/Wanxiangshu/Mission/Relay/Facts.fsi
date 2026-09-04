namespace Wanxiangshu.Mission.Relay

[<RequireQualifiedAccess>]
type RelayEvent =
    | RoadOpened of RoadId * AuthorityRevision
    | IncumbencyOpened of IncumbencyId * WorkspaceSnapshotId * BatonSource
    | AssessmentCommitted of
        AssessmentId * AssessmentBinding * WorkspaceSnapshotId * AuthorityRevision * ScoreVector
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

