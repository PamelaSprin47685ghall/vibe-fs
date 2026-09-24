namespace Wanxiangshu.Participant.Cognition

open Wanxiangshu.Foundation.Identity

/// The folded view of one cognitive owner's workspace: current canvas reference,
/// current declaration, phase ordinal, and the identity of the last committed tool
/// call so a replay can be answered without re-running jq.
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

    let empty: CognitiveProjection =
        { SnapshotRef = None
          SnapshotDigest = None
          Todos = []
          Ordinal = 0L
          LastToolCallId = None
          LastInputDigest = None
          LastSnapshotRef = None
          LastSnapshotDigest = None }

    /// The commit's snapshot identity is recorded field by field. The previous
    /// identity is NOT carried forward: the projection answers "what is current",
    /// and result retirement names committed facts from the journal, not from a
    /// rolling two-slot cache that would drift the moment a rollback happens.
    let apply (commit: AssumePhaseCommitted) (state: CognitiveProjection) : CognitiveProjection =
        { state with
            SnapshotRef = Some commit.SnapshotRef
            SnapshotDigest = Some commit.SnapshotDigest
            Ordinal = commit.Ordinal
            LastToolCallId = Some commit.ToolCallId
            LastInputDigest = Some commit.InputDigest
            LastSnapshotRef = Some commit.SnapshotRef
            LastSnapshotDigest = Some commit.SnapshotDigest }

    let hasCommitted (state: CognitiveProjection) = state.SnapshotRef.IsSome
