namespace Wanxiangshu.Sphinx.V2.Core

/// Strongly typed inquiry identities. Every wrapper is a single-case union over a
/// validated string; the wrap constructor is private so a value can only be built
/// through the module functions that enforce the invariant. Two different id kinds
/// never compare equal, so a NodeId can never be silently used as a WorkId.
///
/// WHAT[sphinx-v2-014]: revision is a checked non-negative integer in F# and a
/// decimal string on the wire, so JavaScript big-integer drift cannot produce two
/// distinct revisions that compare equal.

[<Struct>]
type InquiryId = private InquiryId of string

[<Struct>]
type GoalId = private GoalId of string

[<Struct>]
type SnapshotId = private SnapshotId of string

[<Struct>]
type PlanId = private PlanId of string

[<Struct>]
type WorkId = private WorkId of string

[<Struct>]
type AttemptId = private AttemptId of string

[<Struct>]
type RoundId = private RoundId of string

[<Struct>]
type ObservationId = private ObservationId of string

[<Struct>]
type DecisionId = private DecisionId of string

[<Struct>]
type CertificateId = private CertificateId of string

[<Struct>]
type EventId = private EventId of string

[<Struct>]
type NodeId = private NodeId of string

[<Struct>]
type EdgeId = private EdgeId of string

[<Struct>]
type ArtifactRef = private ArtifactRef of string

/// Monotonic content revision of an immutable artifact. Revision 0 is the origin.
type Revision = private Revision of int64

/// Attempt counter inside one work identity. Starts at 1; a retry is a new attempt.
type Attempt = private Attempt of int64

/// Logical fence of one attempt: retries and late results carry different fences.
type Fence = private Fence of string

module InquiryId =
    val tryCreate: string -> Result<InquiryId, string>
    val create: string -> InquiryId
    val value: InquiryId -> string

module GoalId =
    val tryCreate: string -> Result<GoalId, string>
    val create: string -> GoalId
    val value: GoalId -> string

module SnapshotId =
    val tryCreate: string -> Result<SnapshotId, string>
    val create: string -> SnapshotId
    val value: SnapshotId -> string

module PlanId =
    val tryCreate: string -> Result<PlanId, string>
    val create: string -> PlanId
    val value: PlanId -> string

module WorkId =
    val tryCreate: string -> Result<WorkId, string>
    val create: string -> WorkId
    val value: WorkId -> string

module AttemptId =
    val tryCreate: string -> Result<AttemptId, string>
    val create: string -> AttemptId
    val value: AttemptId -> string

module RoundId =
    val tryCreate: string -> Result<RoundId, string>
    val create: string -> RoundId
    val value: RoundId -> string

module ObservationId =
    val tryCreate: string -> Result<ObservationId, string>
    val create: string -> ObservationId
    val value: ObservationId -> string

module DecisionId =
    val tryCreate: string -> Result<DecisionId, string>
    val create: string -> DecisionId
    val value: DecisionId -> string

module CertificateId =
    val tryCreate: string -> Result<CertificateId, string>
    val create: string -> CertificateId
    val value: CertificateId -> string

module EventId =
    val tryCreate: string -> Result<EventId, string>
    val create: string -> EventId
    val value: EventId -> string

module NodeId =
    val tryCreate: string -> Result<NodeId, string>
    val create: string -> NodeId
    val value: NodeId -> string

module EdgeId =
    val tryCreate: string -> Result<EdgeId, string>
    val create: string -> EdgeId
    val value: EdgeId -> string

module ArtifactRef =
    val tryCreate: string -> Result<ArtifactRef, string>
    val create: string -> ArtifactRef
    val value: ArtifactRef -> string

module Revision =
    val origin: Revision
    val tryCreate: int64 -> Result<Revision, string>
    val create: int64 -> Revision
    val value: Revision -> int64
    val next: Revision -> Revision

module Attempt =
    val first: Attempt
    val tryCreate: int64 -> Result<Attempt, string>
    val create: int64 -> Attempt
    val value: Attempt -> int64
    val next: Attempt -> Attempt

module Fence =
    val tryCreate: string -> Result<Fence, string>
    val create: string -> Fence
    val value: Fence -> string

type CoreError = { Code: string; Message: string }
