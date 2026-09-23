namespace Wanxiangshu.Sphinx.V2.Runtime

open Wanxiangshu.Sphinx.V2.Core

type IncludedArtifact = { Ref: ArtifactRef; Revision: Revision; ContentHash: string }

type SnapshotExclusion = { ItemId: string; Handling: string }

type ContextSnapshot =
    { SnapshotId: SnapshotId
      /// Canonical hash of the whole snapshot record.
      ContentHash: string
      GoalId: GoalId
      GoalRevision: Revision
      Included: IncludedArtifact list
      Excluded: SnapshotExclusion list
      /// Version of the selector/summarizer that produced this snapshot.
      ContextVersion: string
      /// measurement | intervention | generation | rendering.
      Purpose: string
      VisibilityPolicy: string
      /// Hash of the bytes actually shown to a model, not of the source material.
      ModelVisibleBytesHash: string }

type SnapshotError = { Code: string; Message: string }

module Context =
    val tryValidate: ContextSnapshot -> Result<ContextSnapshot, SnapshotError>

    /// What a worker sees. Never carries the opaque-label to real-candidate mapping.
    val publicEnvelopeView: ContextSnapshot -> string -> string -> (string * string) list

    /// What the host keeps. Never crosses to a worker.
    val privateTicketView:
        string -> string -> string -> (string * string) list -> (string * string) list
