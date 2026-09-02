namespace Wanxiangshu.Mission.Obligation.Todo

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// Durable half of the GrandRewrite Magic Todo membrane.
///
/// before: localize the persisted ToolPart, validate
/// `{planComplete,workingOn,obligations:[{name,work}]}` input, write canonical
/// obligation bodies, append Prepared, then expose only
/// legacy sink rows to the builtin executor. after/recovery proves physical
/// success against that receipt before Accepted.
module MagicTodoMembrane =

    [<RequireQualifiedAccess>]
    type PreparedBridgeAcceptance =
        | AwaitingAcceptance
        | Accepted of outputDigest: string

    type PreparedBridge =
        { ManagerSessionId: SessionId
          ManagerLifeId: ManagerLifeId
          Prepared: TodoWritePrepared
          PreparedFactRef: EventId
          BaseObligations: ObligationList
          SubmittedObligations: ObligationList
          Acceptance: PreparedBridgeAcceptance }

    type AcceptOutcome = { EnrichedResult: string }

    /// DSL-class: Decision
    [<RequireQualifiedAccess>]
    type PrepareRejection =
        | NoOpenManagerLife
        | UnexpectedToolName of actual: string
        | SnapshotInputMismatch
        | Admission of MagicTodoReject
        | BlobRead of reason: string
        | BlobWrite of reason: string
        | BlobDigestMismatch of label: string
        | BlobDecode of reason: string
        | JournalAppend of reason: string
        | ProjectionInconsistent of reason: string

    [<RequireQualifiedAccess>]
    type AcceptRejection =
        | InputDigestMismatch
        | OutputDigestMismatch
        | JournalAppend of reason: string

    val prepare:
        journal: AgentJournal ->
        managerSessionId: SessionId ->
        locality: MagicTodoLocality.LocalizedToolCall ->
        providerInputDigest: string ->
        planCompleteDeclared: bool ->
        submitted: ObligationList ->
            Task<Result<PreparedBridge, PrepareRejection>>

    val accept:
        journal: AgentJournal ->
        bridge: PreparedBridge ->
        physical: PhysicalSuccessEvidence ->
        observedInputDigest: string ->
        observedOutputDigest: string ->
            Task<Result<AcceptOutcome, AcceptRejection>>

/// Physical OpenCode V1 hook overlay for Magic Todo. The Host builtin remains
/// the executor/compatibility sink; this layer owns definition, durable prepare,
/// physical-success accept, and model-visible result enrichment.
module MagicTodoHostHooks =

    type HookSet =
        { Definition: obj -> obj -> unit
          Before: obj -> obj -> Task<unit>
          After: obj -> obj -> Task<unit> }

    val create: journal: AgentJournal option -> snapshot: ISessionSnapshotPort option -> HookSet
