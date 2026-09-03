namespace Wanxiangshu.Sphinx.Core

open System
open Fable.Core
open Wanxiangshu.Foundation

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

module private OpaqueId =

    let tryCreate name prefix wrap (value: string) =
        if String.IsNullOrWhiteSpace value then
            Error(sprintf "%s must not be blank" name)
        elif not (value.StartsWith(prefix, StringComparison.Ordinal)) then
            Error(sprintf "%s must start with %s" name prefix)
        elif value |> Seq.exists Char.IsWhiteSpace then
            Error(sprintf "%s must not contain whitespace" name)
        else
            Ok(wrap value)

    let create tryCreate value =
        match tryCreate value with
        | Ok result -> result
        | Error message -> invalidArg "value" message

module InquiryId =
    let tryCreate value =
        OpaqueId.tryCreate "InquiryId" "iq_" InquiryId value

    let create value = OpaqueId.create tryCreate value
    let value (InquiryId value) = value

module EventId =
    let tryCreate value =
        OpaqueId.tryCreate "EventId" "ev" EventId value

    let create value = OpaqueId.create tryCreate value
    let value (EventId value) = value

module NodeId =
    let tryCreate value =
        OpaqueId.tryCreate "NodeId" "n" NodeId value

    let create value = OpaqueId.create tryCreate value
    let value (NodeId value) = value

module EdgeId =
    let tryCreate value =
        OpaqueId.tryCreate "EdgeId" "e" EdgeId value

    let create value = OpaqueId.create tryCreate value
    let value (EdgeId value) = value

module WorkId =
    let tryCreate value =
        OpaqueId.tryCreate "WorkId" "work_" WorkId value

    let create value = OpaqueId.create tryCreate value
    let value (WorkId value) = value

module BranchId =
    let tryCreate value =
        OpaqueId.tryCreate "BranchId" "branch_" BranchId value

    let create value = OpaqueId.create tryCreate value
    let value (BranchId value) = value

module BlindToken =
    let tryCreate value =
        OpaqueId.tryCreate "BlindToken" "blind" BlindToken value

    let create value = OpaqueId.create tryCreate value
    let value (BlindToken value) = value

type SchemaRef = { Id: string; Hash: string }

type JsonEnvelope =
    { Schema: SchemaRef
      CanonicalPayload: string }

module JsonEnvelope =

    let private validateSchema schema =
        if String.IsNullOrWhiteSpace schema.Id then
            Error "schema id must not be blank"
        elif String.IsNullOrWhiteSpace schema.Hash then
            Error "schema hash must not be blank"
        else
            Ok schema

    let create schema payload =
        validateSchema schema
        |> Result.map (fun valid ->
            { Schema = valid
              CanonicalPayload = CanonicalJson.canonicalJson payload })

    let private canonicalizePayload (valid: SchemaRef) (payload: string) : Result<JsonEnvelope, string> =
        try
            Ok
                { Schema = valid
                  CanonicalPayload = payload |> JS.JSON.parse |> CanonicalJson.canonicalJson }
        with _ ->
            Error "payload is not valid JSON"

    let ofCanonical schema payload =
        match validateSchema schema with
        | Error error -> Error error
        | Ok valid -> canonicalizePayload valid payload

    let payload envelope = JS.JSON.parse envelope.CanonicalPayload

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
    | Executing of LeaseProof
    | InputRequired of LeaseProof
    | Succeeded of CompletionProof
    | Failed of CompletionProof
    | Cancelled of CompletionProof
    | Superseded of WorkId

type WorkItem = { Spec: WorkSpec; State: WorkState }

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

type CertificatePatch = { Certificate: ValueCertificate }

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

type CoreError = { Code: string; Message: string }
