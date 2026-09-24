namespace Wanxiangshu.Participant.Cognition

open Wanxiangshu.Foundation.Identity

type AssumePhaseCommitted =
    { OwnerKey: string
      SessionId: SessionId
      IncumbencyId: string
      ToolCallId: ToolCallId
      Ordinal: int64
      InputDigest: string
      PredecessorOrdinal: int64 option
      SnapshotRef: BlobRef
      SnapshotDigest: BlobDigest
      RendererVersion: string }

[<RequireQualifiedAccess>]
module AssumeFactCases =
    type T = AssumePhaseCommitted of AssumePhaseCommitted
