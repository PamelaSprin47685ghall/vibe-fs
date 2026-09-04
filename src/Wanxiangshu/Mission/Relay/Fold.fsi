namespace Wanxiangshu.Mission.Relay

type RelayState

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

module Fold =
    val empty: RelayState
    val apply: state: RelayState -> roadId: RoadId -> transaction: RelayTransaction -> Result<RelayState, string>
    val view: state: RelayState -> roadId: RoadId -> RoadView option

module Decision =
    val openIncumbency:
        state: RelayState ->
        roadId: RoadId ->
        incumbentId: IncumbencyId ->
        snapshotId: WorkspaceSnapshotId ->
        authorityRevision: AuthorityRevision ->
        source: BatonSource ->
        Result<RelayState, string>

    val assess:
        state: RelayState ->
        roadId: RoadId ->
        incumbentId: IncumbencyId ->
        assessmentId: AssessmentId ->
        binding: AssessmentBinding ->
        snapshotId: WorkspaceSnapshotId ->
        authorityRevision: AuthorityRevision ->
        scores: ScoreVector ->
        Result<RelayState, string>

    val invalidateCertificate: state: RelayState -> roadId: RoadId -> reason: string -> Result<RelayState, string>

    val retire:
        state: RelayState ->
        roadId: RoadId ->
        incumbentId: IncumbencyId ->
        retirement: RetirementSummary ->
        Result<RelayState, string>

    val activateSuccessor:
        state: RelayState ->
        roadId: RoadId ->
        predecessor: RetirementId ->
        incumbentId: IncumbencyId ->
        snapshotId: WorkspaceSnapshotId ->
        authorityRevision: AuthorityRevision ->
        Result<RelayState, string>

