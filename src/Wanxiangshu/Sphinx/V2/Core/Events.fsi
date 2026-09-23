namespace Wanxiangshu.Sphinx.V2.Core

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

type UsageSettledBody = { Usage: SettledUsage }

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
    {
        WorkId: WorkId
        Attempt: Attempt
        Fence: Fence
        ObservationId: ObservationId
        /// Canonical response bytes exactly as the worker returned them (CB-03: raw
        /// answers stay replayable; re-fitting never rewrites them).
        CanonicalResult: string
        ResultSchema: SchemaRef
        ClusterId: string
    }

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
    { Patch: GraphPatch; PluginRef: string }

type CertificateSlotsPatchedBody = { Patches: CertificateSlotPatch list }

type CertificateInvalidatedBody =
    { Invalidation: JsonEnvelope
      Reason: string }

type DecisionRecordedBody = { Decision: JsonEnvelope }

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

type InquiryStatusChangedBody = { Status: string; Reason: string }

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

type TransitionBatch =
    { SchemaVersion: string
      InquiryId: InquiryId
      PreviousRevision: Revision
      PreviousHead: EventId option
      Revision: Revision
      CommandId: string
      CommandFingerprint: string
      Events: InquiryEventBody list
      PostStateFingerprint: string option }

type InquiryEvent =
    { Id: EventId
      InquiryId: InquiryId
      Revision: Revision
      Parent: EventId option
      BatchIndex: int
      Body: InquiryEventBody }

[<RequireQualifiedAccess>]
module SphinxV2EventTypes =
    val transition: string
    val all: string list
    val isKnown: string -> bool
