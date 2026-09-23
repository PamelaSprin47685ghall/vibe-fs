namespace Wanxiangshu.Sphinx.V2.Core

[<RequireQualifiedAccess>]
type InquiryStatus =
    | Active
    | InputRequired of authorization: string
    | Suspended of reason: string
    | Cancelling
    | StopReached of stopReason: string
    | Failed of reason: string
    | Cancelled of reason: string

type RoundRecord =
    { RoundId: RoundId
      ScopeId: string
      ExpectedWork: Set<WorkId>
      ReceivedWork: Set<WorkId>
      TerminalWork: Set<WorkId>
      Closed: bool
      Outcome: string option }

type InterpretationRecord =
    { ObservationId: ObservationId
      WorkId: WorkId
      Attempt: Attempt
      InterpretationId: string option
      PluginRef: string option
      Status: string
      Reason: string option }

/// A reservation keyed by work identity. A tuple key would work in F# but not as a
/// canonical hash participant, and this record is hashed.
type ReservationKey = { WorkId: WorkId; Attempt: Attempt }

/// An overrun fact. Recorded, never absorbed into a rejection.
type OverrunFact = { WorkId: WorkId; Attempt: Attempt; Resources: Map<string, float> }

/// A physical binding recovered from a Host. Never participates in semantic hashing.
type PhysicalBinding =
    { WorkId: WorkId
      Attempt: Attempt
      DispatchIntentId: string
      PhysicalRef: string option
      Receipt: string option }

type InquiryState =
    { Id: InquiryId
      ApiVersion: string
      Revision: Revision
      EventHead: EventId option
      Goal: GoalSpec
      ResourceSpecs: ResourceSpec list
      RenderReserve: Map<string, float>
      ConfigHash: string
      ProfileRef: string
      Graph: Map<NodeId, GraphNode>
      Edges: Map<EdgeId, HyperEdge>
      /// Certificates are addressed by their full scope key, never by node alone.
      Certificates: Map<string, CertificateSlotPatch list>
      /// Outstanding reservations: work identity -> reserved resources.
      Work: Map<WorkId, WorkItem>
      Reservations: Map<string, ReservationKey * Map<string, float>>
      SettledUsage: Map<string, float>
      SettledMoneyMinor: int64
      /// Overruns recorded as facts, never absorbed.
      Overruns: OverrunFact list
      Observations: Map<string, ResultAcceptedBody>
      Interpretations: Map<string, InterpretationRecord>
      Rounds: Map<RoundId, RoundRecord>
      Decisions: Map<string, string>
      Answer: AnswerCommittedBody option
      /// Command identity -> revision, for idempotent retry of a control command.
      CommandReceipts: Map<string, Revision>
      PhysicalBindings: Map<string, PhysicalBinding>
      Status: InquiryStatus }

module InquiryState =
    val commandRevision: InquiryState -> string -> Revision option

    val certificateKey: string -> string -> string -> string -> string

    val reservationKey: ReservationKey -> string

    val trySlots:
        InquiryState -> string -> string -> string -> string -> CertificateSlotPatch list option

    val isTerminal: InquiryStatus -> bool
    val readyWork: InquiryState -> WorkItem list
