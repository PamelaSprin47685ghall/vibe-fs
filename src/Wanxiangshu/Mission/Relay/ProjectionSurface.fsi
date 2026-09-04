namespace Wanxiangshu.Mission.Relay

module ProjectionSurface =
    val maxRisks: int
    val maxEvidenceRefs: int
    val baton: obj -> obj
    val applyCut: messages: obj array -> cutSequence: int -> staleRunIds: string array -> obj
    val successorContext: rootRequest: string -> authorityRevision: string -> snapshotId: string -> baton: string -> obj

