namespace Wanxiangshu.Mission.Relay

type RoadId = private RoadId of string
type IncumbencyId = private IncumbencyId of string
type WorkspaceSnapshotId = private WorkspaceSnapshotId of string
type AuthorityRevision = private AuthorityRevision of string
type AssessmentId = private AssessmentId of string
type QualityCertificateId = private QualityCertificateId of string
type RetirementId = private RetirementId of string

module RoadId =
    let create value = RoadId value
    let value (RoadId value) = value

module IncumbencyId =
    let create value = IncumbencyId value
    let value (IncumbencyId value) = value

module WorkspaceSnapshotId =
    let create value = WorkspaceSnapshotId value
    let value (WorkspaceSnapshotId value) = value

module AuthorityRevision =
    let create value = AuthorityRevision value
    let value (AuthorityRevision value) = value

module AssessmentId =
    let create value = AssessmentId value
    let value (AssessmentId value) = value

module QualityCertificateId =
    let create value = QualityCertificateId value
    let value (QualityCertificateId value) = value

module RetirementId =
    let create value = RetirementId value
    let value (RetirementId value) = value

/// The single gate-kind vocabulary for the manager loop prompt, shared by
/// the Change sender and the projection cut so the two never drift apart.
module ManagerLoopGate =
    let gateKind (retirementId: RetirementId) =
        "manager-loop:" + RetirementId.value retirementId

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
    let all =
        [ ScoreDimension.LanguageAlgorithms
          ScoreDimension.Simplicity
          ScoreDimension.Structure
          ScoreDimension.Granularity
          ScoreDimension.TestsEvidence
          ScoreDimension.LogicReliabilityBoundaries
          ScoreDimension.CallerErgonomics
          ScoreDimension.Completeness ]

    let fieldName dimension =
        match dimension with
        | ScoreDimension.LanguageAlgorithms -> "language_algorithms"
        | ScoreDimension.Simplicity -> "simplicity"
        | ScoreDimension.Structure -> "structure"
        | ScoreDimension.Granularity -> "granularity"
        | ScoreDimension.TestsEvidence -> "tests_evidence"
        | ScoreDimension.LogicReliabilityBoundaries -> "logic_reliability_boundaries"
        | ScoreDimension.CallerErgonomics -> "caller_ergonomics"
        | ScoreDimension.Completeness -> "completeness"

type ScoreVector = private ScoreVector of Map<ScoreDimension, int>

module ScoreVector =
    let tryCreate scores =
        if List.length scores <> List.length ScoreDimension.all then
            Error "ScoreVector requires exactly eight scores."
        elif scores |> List.exists (fun score -> score < 0 || score > 10) then
            Error "Every score must be an integer from 0 through 10."
        else
            List.zip ScoreDimension.all scores |> Map.ofList |> ScoreVector |> Ok

    let values (ScoreVector scores) =
        ScoreDimension.all |> List.map (fun dimension -> scores.[dimension])

    let score dimension (ScoreVector scores) = scores.[dimension]
    let allPerfect vector = values vector |> List.forall ((=) 10)

    let lowDimensions vector =
        ScoreDimension.all |> List.filter (fun dimension -> score dimension vector < 10)

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

[<RequireQualifiedAccess>]
type RetirementOutcome =
    | Continue
    | Accepted of QualityCertificateId

type ProjectionCut =
    { ProviderRunId: string
      ToolCallId: string }

type RetirementSummary =
    { Id: RetirementId
      IncumbencyId: IncumbencyId
      SnapshotId: WorkspaceSnapshotId
      AuthorityRevision: AuthorityRevision
      ProjectionCut: ProjectionCut
      Outcome: RetirementOutcome }
