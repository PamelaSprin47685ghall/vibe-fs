namespace Wanxiangshu.Sphinx.V2.Core

type GoalProjection =
    { GoalId: string
      Revision: int64
      OriginalText: string
      Constraints: string list
      MaterialRefs: string list
      Authorization: string
      Amendments: AmendmentProjection list }

and AmendmentProjection =
    { AuthorizedBy: string
      Revision: int64
      AddedConstraints: string list
      ReplacedText: string option }

type NodeProjection =
    { Id: string
      Role: string
      Kind: string
      SchemaId: string
      SchemaHash: string
      Revision: int64
      ContentHash: string
      Payload: string }

type EdgeProjection =
    { Id: string
      Tails: string list
      Heads: string list
      Relation: string
      Revision: int64
      Payload: string option
      PayloadSchemaId: string option }

type SlotProjection =
    { Slot: string
      Producer: string
      SchemaId: string
      SchemaHash: string
      Revision: int64
      Status: string
      Guarantee: string
      Payload: string
      ExpectedBase: int64 }

type CertificateProjection =
    { Key: string
      Slots: SlotProjection list }

type WorkProjection =
    { Id: string
      Round: string option
      Plan: string
      Producer: string
      Capability: string
      Attempt: int64
      State: string
      Dependencies: string list
      ConflictKeys: string list
      Input: string option }

type ObservationProjection =
    { Key: string
      Work: string
      Attempt: int64
      Observation: string
      Cluster: string
      SchemaId: string
      SchemaHash: string
      Result: string }

type InterpretationProjection =
    { Key: string
      Status: string
      InterpretationId: string option
      Plugin: string option }

type RoundProjection =
    { Round: string
      Scope: string
      Expected: string list
      Received: string list
      Terminal: string list
      Closed: bool
      Outcome: string option }

type OverrunProjection =
    { Work: string
      Attempt: int64
      Resources: (string * float) list }

type BudgetProjection =
    { SettledUsage: (string * float) list
      SettledMoneyMinor: int64
      Overruns: OverrunProjection list
      Reservations: (string * (string * float) list) list }

type AnswerProjection =
    { RenderWork: string
      AnswerRef: string
      StopReason: string }

type SemanticProjection =
    { ApiVersion: string
      Goal: GoalProjection
      Graph: NodeProjection list
      Edges: EdgeProjection list
      Certificates: CertificateProjection list
      Work: WorkProjection list
      Observations: ObservationProjection list
      Interpretations: InterpretationProjection list
      Rounds: RoundProjection list
      Decisions: (string * string) list
      Budget: BudgetProjection
      Answer: AnswerProjection option }

module Projection =
    val canonical: obj -> string

    /// Semantic projection: excludes physical session ids, transport cursors and
    /// wall-clock timestamps, so it can claim Host-independence (C-11, C-12).
    val semanticProjection: InquiryState -> SemanticProjection
    val semanticHash: InquiryState -> string

    /// Covers every materialized field, including the physical bindings recovery needs.
    val stateHash: InquiryState -> string

    /// Covers the accepted canonical envelopes in their exact order.
    val traceHash: string list -> string
