namespace Wanxiangshu.Mission.Relay

type RelayState

type RoadView =
    { AuthorityRevision: AuthorityRevision
      AuthorityRevisions: AuthorityRevision list
      AuthorityMessageIds: PhysicalUserMessageId list
      ActiveIncumbency: IncumbencyId option
      ActivePhase: IncumbencyPhase option
      ActiveSnapshotId: WorkspaceSnapshotId option
      ActiveAuthorityRevision: AuthorityRevision option
      ActiveCleanupBlockerDigest: string option
      AcceptedAssessmentTransport: (string * string) option
      RetiredIncumbencies: IncumbencyId list
      RetiredProviderRunIds: Set<string>
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

    val advanceAuthority:
        state: RelayState ->
        roadId: RoadId ->
        incumbentId: IncumbencyId ->
        expected: AuthorityRevision ->
        next: AuthorityRevision ->
        authorityMessageId: PhysicalUserMessageId ->
        snapshotId: WorkspaceSnapshotId ->
            Result<RelayState, string>

    val invalidateCertificate: state: RelayState -> roadId: RoadId -> reason: string -> Result<RelayState, string>

    val blockCleanup:
        state: RelayState ->
        roadId: RoadId ->
        incumbentId: IncumbencyId ->
        blockerDigest: string ->
            Result<RelayState, string>

    val retire:
        state: RelayState ->
        roadId: RoadId ->
        incumbentId: IncumbencyId ->
        retirement: RetirementSummary ->
            Result<RelayState, string>
