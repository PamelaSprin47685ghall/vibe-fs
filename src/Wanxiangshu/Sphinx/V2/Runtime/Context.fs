namespace Wanxiangshu.Sphinx.V2.Runtime

open Wanxiangshu.Sphinx.V2.Core

/// The context snapshot bound into every round.
///
/// WHAT[sphinx-v2-006]: a snapshot is not "a common prefix" in prose. It names the goal
/// revision, the exact artifact revisions with their content hashes, what was excluded
/// and how, which summarizer produced the digest, the purpose of the round, the
/// visibility policy and the hash of the bytes the model actually saw. Two balloons that
/// were shown different byte counts are not comparable measurements, and the manifest is
/// where that becomes visible.

type IncludedArtifact =
    { Ref: ArtifactRef
      Revision: Revision
      ContentHash: string }

type SnapshotExclusion = { ItemId: string; Handling: string }

type ContextSnapshot =
    {
        SnapshotId: SnapshotId
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
        ModelVisibleBytesHash: string
    }

type SnapshotError = { Code: string; Message: string }

module Context =

    let private error code message : Result<'value, SnapshotError> =
        Error { Code = code; Message = message }

    /// A snapshot needs its own id, a content hash, and at least one purpose the round
    /// can be held to. The excluded list may be empty; the handling rule may not.
    let tryValidate (snapshot: ContextSnapshot) : Result<ContextSnapshot, SnapshotError> =
        if System.String.IsNullOrWhiteSpace snapshot.ContentHash then
            error "invalid-snapshot" "snapshot content hash must not be blank"
        elif System.String.IsNullOrWhiteSpace snapshot.ModelVisibleBytesHash then
            error "invalid-snapshot" "snapshot must record the hash of model-visible bytes"
        elif snapshot.Included |> List.isEmpty then
            error "invalid-snapshot" "snapshot must include at least one artifact revision"
        elif
            snapshot.Excluded
            |> List.exists (fun exclusion -> System.String.IsNullOrWhiteSpace exclusion.Handling)
        then
            error "invalid-snapshot" "every excluded item needs a handling rule"
        else
            Ok snapshot

    /// The public envelope a worker sees: goal, visible material, the question, the
    /// response schema, and the declared work limits. It never carries the label map
    /// that links an opaque id back to a real candidate.
    let publicEnvelopeView (snapshot: ContextSnapshot) (question: string) (schemaId: string) : (string * string) list =
        [ ("goalRevision", string (Revision.value snapshot.GoalRevision))
          ("snapshotId", SnapshotId.value snapshot.SnapshotId)
          ("visibleBytesHash", snapshot.ModelVisibleBytesHash)
          ("question", question)
          ("responseSchemaId", schemaId) ]

    /// The private ticket the host keeps: the real mapping, the producer, the schema
    /// reference and the dispatch identity. A worker never receives this.
    let privateTicketView
        (scopeId: string)
        (clusterId: string)
        (dispatchIntentId: string)
        (candidateMapping: (string * string) list)
        : (string * string) list =
        [ ("scopeId", scopeId)
          ("clusterId", clusterId)
          ("dispatchIntentId", dispatchIntentId)
          ("candidateMapping",
           candidateMapping
           |> List.map (fun (opaque, real) -> real + " <- " + opaque)
           |> String.concat ";") ]
