namespace Wanxiangshu.Sphinx.Core

[<Struct>]
type InquiryId = private InquiryId of string

[<Struct>]
type EventId = private EventId of string

[<Struct>]
type NodeId = private NodeId of string

[<Struct>]
type EdgeId = private EdgeId of string

[<Struct>]
type WorkId = private WorkId of string

[<Struct>]
type BranchId = private BranchId of string

[<Struct>]
type BlindToken = private BlindToken of string

module InquiryId =
    val tryCreate: string -> Result<InquiryId, string>
    val create: string -> InquiryId
    val value: InquiryId -> string

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

module WorkId =
    val tryCreate: string -> Result<WorkId, string>
    val create: string -> WorkId
    val value: WorkId -> string

module BranchId =
    val tryCreate: string -> Result<BranchId, string>
    val create: string -> BranchId
    val value: BranchId -> string

module BlindToken =
    val tryCreate: string -> Result<BlindToken, string>
    val create: string -> BlindToken
    val value: BlindToken -> string

type SchemaRef =
    { Id: string
      Hash: string }

type JsonEnvelope =
    { Schema: SchemaRef
      CanonicalPayload: string }

module JsonEnvelope =
    val create<'a> : SchemaRef -> 'a -> Result<JsonEnvelope, string>
    val ofCanonical: SchemaRef -> string -> Result<JsonEnvelope, string>
    val payload: JsonEnvelope -> obj

type PluginRef =
    { Id: string
      Release: string
      AbiHash: string }

type PluginLockEntry =
    { Plugin: PluginRef
      Capabilities: Set<string>
      Dependencies: Set<string>
      Schemas: Map<string, SchemaRef> }

type GraphNode =
    { Id: NodeId
      Kind: string
      Payload: JsonEnvelope
      Revision: int64 }

type HyperEdge =
    { Id: EdgeId
      Tails: Set<NodeId>
      Heads: Set<NodeId>
      Relation: string
      Payload: JsonEnvelope option }

type CertificateGuarantee =
    | DeterministicInclusion of assumptions: Set<string>
    | ProbabilisticCoverage of level: float * error: float * assumptions: Set<string> * scope: string
    | OrdinalModel of assumptions: Set<string>
    | ResidualOnly

type ValueCertificate =
    { NodeId: NodeId
      Semantics: PluginRef option
      Exact: JsonEnvelope option
      LowerEnvelope: JsonEnvelope option
      UpperEnvelope: JsonEnvelope option
      SampleSummary: JsonEnvelope option
      OrdinalConstraints: JsonEnvelope list
      LatentPosterior: JsonEnvelope option
      Residual: JsonEnvelope option
      Guarantees: Map<string, CertificateGuarantee>
      WitnessEvents: EventId list
      DerivationEvents: EventId list
      Revision: int64 }

type ResourceBudget = Map<string, float>

type WorkSpec =
    { Id: WorkId
      BranchId: BranchId
      Attempt: int
      Producer: PluginRef option
      Capability: string
      Input: JsonEnvelope option
      OutputSchema: SchemaRef option
      Dependencies: Set<WorkId>
      ConflictKeys: Set<string>
      BlindToken: BlindToken option
      RandomSeed: string
      Budget: ResourceBudget }

type LeaseProof =
    { Attempt: int
      Fence: string
      Session: string option }

type CompletionProof =
    { Attempt: int
      EventId: EventId option
      Detail: JsonEnvelope option }

type WorkState =
    | Planned
    | Ready
    | Leased of LeaseProof
    | Running of LeaseProof
    | InputRequired of LeaseProof
    | Succeeded of CompletionProof
    | Failed of CompletionProof
    | Cancelled of CompletionProof
    | Superseded of WorkId

type WorkItem =
    { Spec: WorkSpec
      State: WorkState }

type InquiryStatus =
    | Active
    | InputRequired
    | Suspended of string
    | Cancelling
    | Completed
    | Failed of string
    | Cancelled

type GraphPatch =
    { UpsertNodes: GraphNode list
      RemoveNodes: NodeId list
      UpsertEdges: HyperEdge list
      RemoveEdges: EdgeId list }

type CertificatePatch =
    { Certificate: ValueCertificate }

type ProtocolBinding =
    { RootSnapshotHash: string
      BranchId: BranchId
      WorkId: WorkId
      Attempt: int
      PluginLock: PluginLockEntry list
      Schema: SchemaRef
      PromptId: string
      QuestionId: string
      Wording: JsonEnvelope
      Permutation: JsonEnvelope
      Treatment: string
      BlindToken: BlindToken
      RandomSeed: string
      Model: JsonEnvelope
      Sampling: JsonEnvelope
      Usage: JsonEnvelope
      Payload: JsonEnvelope }

type CoreEventBody =
    | InquiryCreated of root: JsonEnvelope * lock: PluginLockEntry list * budget: ResourceBudget
    | PluginSetBound of lock: PluginLockEntry list
    | GraphPatched of GraphPatch
    | WorkPlanned of WorkSpec list
    | WorkTransitioned of work: WorkSpec * fromState: string * nextState: WorkState
    | ObservationAccepted of ProtocolBinding
    | CertificatePatched of CertificatePatch
    | BudgetDebited of ResourceBudget
    | InquiryStatusChanged of InquiryStatus
    | AnswerCommitted of JsonEnvelope

type InquiryEvent =
    { Id: EventId
      InquiryId: InquiryId
      Revision: int64
      Parent: EventId option
      Body: CoreEventBody }

type InquiryState =
    { Id: InquiryId
      Revision: int64
      EventHead: EventId
      Graph: Map<NodeId, GraphNode>
      Edges: Map<EdgeId, HyperEdge>
      Certificates: Map<NodeId, ValueCertificate>
      Work: Map<WorkId, WorkItem>
      PluginLock: Map<string, PluginLockEntry>
      Budget: ResourceBudget
      Observations: Map<WorkId * int, string>
      Status: InquiryStatus
      Answer: JsonEnvelope option }

type CoreError =
    { Code: string
      Message: string }
