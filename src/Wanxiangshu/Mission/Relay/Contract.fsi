namespace Wanxiangshu.Mission.Relay

type RoadId
type IncumbencyId
type WorkspaceSnapshotId
type AuthorityRevision
type AssessmentId
type QualityCertificateId
type RetirementId
type BatonId
type ProjectionCutId

module RoadId =
    val create: string -> RoadId
    val value: RoadId -> string

module IncumbencyId =
    val create: string -> IncumbencyId
    val value: IncumbencyId -> string

module WorkspaceSnapshotId =
    val create: string -> WorkspaceSnapshotId
    val value: WorkspaceSnapshotId -> string

module AuthorityRevision =
    val create: string -> AuthorityRevision
    val value: AuthorityRevision -> string

module AssessmentId =
    val create: string -> AssessmentId
    val value: AssessmentId -> string

module QualityCertificateId =
    val create: string -> QualityCertificateId
    val value: QualityCertificateId -> string

module RetirementId =
    val create: string -> RetirementId
    val value: RetirementId -> string

module BatonId =
    val create: string -> BatonId
    val value: BatonId -> string

module ProjectionCutId =
    val create: string -> ProjectionCutId
    val value: ProjectionCutId -> string

[<RequireQualifiedAccess>]
type BatonSource =
    | ExistingWorld
    | Retirement of RetirementId

[<RequireQualifiedAccess>]
type ScoreDimension =
    | LanguageAlgorithms
    | Simplicity
    | Structure
    | Granularity
    | TestsEvidence
    | LogicReliabilityBoundaries
    | CallerErgonomics
    | Completeness

module ScoreDimension =
    val all: ScoreDimension list
    val fieldName: ScoreDimension -> string

type ScoreVector

module ScoreVector =
    val tryCreate: int list -> Result<ScoreVector, string>
    val values: ScoreVector -> int list
    val allPerfect: ScoreVector -> bool
    val lowDimensions: ScoreVector -> ScoreDimension list
    val score: ScoreDimension -> ScoreVector -> int

type AssessmentBinding =
    { PhysicalUserMessageId: string
      ProviderRunId: string
      ToolCallId: string
      NarrativeDigest: string
      PayloadDigest: string
      RootRequestDigest: string
      RequirementSetDigest: string
      EvidenceFrontierDigest: string }

[<RequireQualifiedAccess>]
type IncumbencyPhase =
    | AuditPending
    | WorkOwned
    | PerfectAwaitingRetirement
    | RetirementCleanupBlocked

type QualityCertificate =
    { Id: QualityCertificateId
      AssessmentId: AssessmentId
      IncumbencyId: IncumbencyId
      SnapshotId: WorkspaceSnapshotId
      AuthorityRevision: AuthorityRevision
      Binding: AssessmentBinding
      Valid: bool
      InvalidationReason: string option }

type BatonEnvelope =
    { SchemaVersion: int
      RoadId: string
      FromIncumbencyId: string
      AuthorityRevision: string
      SnapshotId: string
      OpenObligations: string list
      EvidenceRefs: string list }

type ProjectionCut =
    { RetiredIncumbencyId: string
      ThroughProviderRunId: string
      ThroughToolCallId: string
      StaleProviderRunIds: string list }

type RetirementSummary =
    { Id: RetirementId
      IncumbencyId: IncumbencyId
      SnapshotId: WorkspaceSnapshotId
      BatonId: BatonId
      Baton: BatonEnvelope
      ProjectionCutId: ProjectionCutId
      ProjectionCut: ProjectionCut
      SuccessorRequested: bool
      QualityCandidateAccepted: bool }

