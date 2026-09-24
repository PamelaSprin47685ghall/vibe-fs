namespace Wanxiangshu.Participant.Cognition

open Wanxiangshu.Foundation.Identity

/// One committed cognitive phase: the moment the model declared that the previous
/// exploration had settled and a new canvas now governs.
///
/// The event records a completed fact only. Input rejection, jq failure and
/// pre-commit resource refusal never produce one, so a failed call cannot advance
/// the phase ordinal or move an epoch.
type AssumePhaseCommitted =
    {
        OwnerKey: string
        SessionId: SessionId
        IncumbencyId: string
        ToolCallId: ToolCallId
        /// Committed ordinal within the owner. Monotonic; never rewound by a
        /// compaction generation change.
        Ordinal: int64
        /// Digest of the canonical `{ update, todos }` tool input.
        InputDigest: string
        /// Identity of the previous committed phase, or None for the first one.
        PredecessorOrdinal: int64 option
        SnapshotRef: BlobRef
        SnapshotDigest: BlobDigest
        RendererVersion: string
    }

[<RequireQualifiedAccess>]
module AssumeFactCases =
    type T = AssumePhaseCommitted of AssumePhaseCommitted
