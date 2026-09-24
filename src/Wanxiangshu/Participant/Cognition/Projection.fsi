namespace Wanxiangshu.Participant.Cognition

open Wanxiangshu.Foundation.Identity

type CognitiveProjection =
    { SnapshotRef: BlobRef option
      SnapshotDigest: BlobDigest option
      Todos: TodoRow list
      Ordinal: int64
      LastToolCallId: ToolCallId option
      LastInputDigest: string option
      LastSnapshotRef: BlobRef option
      LastSnapshotDigest: BlobDigest option }

[<RequireQualifiedAccess>]
module CognitiveProjection =
    val empty: CognitiveProjection
    val apply: commit: AssumePhaseCommitted -> state: CognitiveProjection -> CognitiveProjection
    val hasCommitted: state: CognitiveProjection -> bool
