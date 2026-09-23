namespace Wanxiangshu.Sphinx.V2.Core

/// The single v2 inquiry event vocabulary.
///
/// WHAT[sphinx-v2-015]: these are run facts, never an epistemic verdict. There is no
/// "HypothesisTrue", no "EvidenceReliable", no "ReflectiveEquilibriumReached": a fold
/// that recorded a truth judgement would be Core making a semantic call, which is the
/// LLM's job. Graph payload meaning belongs to the plugin that produced it; Core only
/// compares identity and hash.

type InquiryCreatedBody =
    { Goal: GoalSpec
      ResourceSpecs: ResourceSpec list
      ProfileRef: string
      ConfigHash: string
      RenderReserve: Map<string, float> }

type RoundOpenedBody =
    { RoundId: RoundId
      ScopeId: string
      ExpectedWork: WorkId list }

type BudgetReservedBody =
    { Reservation: Reservation
      RenderReserve: Map<string, float> }

type UsageSettledBody =
    { Usage: SettledUsage }

type DispatchRequestedBody =
    { Work: WorkSpec
      DispatchIntentId: string
      PublicEnvelope: JsonEnvelope
      PrivateTicket: JsonEnvelope }

type DispatchReceiptRecordedBody =
    { WorkId: WorkId
      Attempt: Attempt
      Fence: Fence
      DispatchIntentId: string
      PhysicalRef: string
      Receipt: JsonEnvelope }

type WorkAttemptTransitionedBody =
    { WorkId: WorkId
      Attempt: Attempt
      Fence: Fence
      FromState: string
      NextState: WorkState
      PhysicalRef: string option }

type ResultAcceptedBody =
    { WorkId: WorkId
      Attempt: Attempt
      Fence: Fence
      ObservationId: ObservationId
      /// Canonical response bytes exactly as the worker returned them (CB-03: raw
      /// answers stay replayable; re-fitting never rewrites them).
      CanonicalResult: string
      ResultSchema: SchemaRef
      ClusterId: string }

type InterpretationPendingBody =
    { ObservationId: ObservationId
      WorkId: WorkId
      Attempt: Attempt }

type InterpretationAppliedBody =
    { ObservationId: ObservationId
      InterpretationId: string
      PluginRef: string
      Delta: JsonEnvelope }

type InterpretationFailedBody =
    { ObservationId: ObservationId
      InterpretationId: string
      PluginRef: string
      Reason: string }

type GraphPatchedBody =
    { Patch: JsonEnvelope
      PluginRef: string }

type CertificateSlotsPatchedBody =
    { Patches: CertificateSlotPatch list }

type CertificateInvalidatedBody =
    { Invalidation: JsonEnvelope
      Reason: string }

type DecisionRecordedBody =
    { Decision: JsonEnvelope }

type AnswerPreparedBody =
    { RenderWorkId: WorkId
      DraftRef: string }

type AnswerCommittedBody =
    { RenderWorkId: WorkId
      AnswerRef: string
      StopReason: string }

type HostTerminalRecordedBody =
    { WorkId: WorkId
      Attempt: Attempt
      Fence: Fence
      Terminal: string
      Receipt: JsonEnvelope }

type InquiryStatusChangedBody =
    { Status: string
      Reason: string }

[<RequireQualifiedAccess>]
type InquiryEventBody =
    | InquiryCreated of InquiryCreatedBody
    | GoalAmended of GoalSpec
    | SnapshotRegistered of JsonEnvelope
    | DecisionScopeOpened of JsonEnvelope
    | RoundOpened of RoundOpenedBody
    | WorkPlanned of WorkSpec list
    | RoundClosed of RoundId * outcome: string
    | BudgetReserved of BudgetReservedBody
    | UsageSettled of UsageSettledBody
    | ReservationReleased of WorkId * Attempt
    | UsageOverrunRecorded of UsageSettledBody
    | DispatchRequested of DispatchRequestedBody
    | DispatchReceiptRecorded of DispatchReceiptRecordedBody
    | WorkAttemptTransitioned of WorkAttemptTransitionedBody
    | HostTerminalRecorded of HostTerminalRecordedBody
    | ResultAccepted of ResultAcceptedBody
    | InterpretationPending of InterpretationPendingBody
    | InterpretationApplied of InterpretationAppliedBody
    | InterpretationFailed of InterpretationFailedBody
    | GraphPatched of GraphPatchedBody
    | CertificateSlotsPatched of CertificateSlotsPatchedBody
    | CertificateInvalidated of CertificateInvalidatedBody
    | DecisionRecorded of DecisionRecordedBody
    | AnswerPrepared of AnswerPreparedBody
    | AnswerCommitted of AnswerCommittedBody
    | CancelRequested of reason: string
    | InquiryCancelled of reason: string
    | InquirySuspended of reason: string
    | InquiryFailed of reason: string
    | InquiryStatusChanged of InquiryStatusChangedBody

/// One canonical envelope carries one whole transition batch (WHAT[sphinx-v2-019]).
/// The batch is the unit of durability: either every event in it is applied or none.
type TransitionBatch =
    { SchemaVersion: string
      InquiryId: InquiryId
      PreviousRevision: Revision
      PreviousHead: EventId option
      Revision: Revision
      CommandId: string
      CommandFingerprint: string
      Events: InquiryEventBody list
      /// Optional self-check value; never a trust source.
      PostStateFingerprint: string option }

type InquiryEvent =
    { Id: EventId
      InquiryId: InquiryId
      Revision: Revision
      Parent: EventId option
      BatchIndex: int
      Body: InquiryEventBody }

/// The registered canonical event type for every v2 Sphinx transition.
[<RequireQualifiedAccess>]
module SphinxV2EventTypes =

    let transition = "sphinx/v2-transition@1"

    let all = [ transition ]

    let isKnown eventType = List.contains eventType all
