namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Repository.Programming.Js

/// Parse the sandbox JSON string into LlmFacing reference data and enforce JS-010.
module JsToolsData =
    val parse: json: string -> Result<obj, JsFailure>

/// The task a delivery serves. The durable witness names the same three
/// references; this layer states them in its own vocabulary because it runs
/// below the host turn and cannot reach the host's witness type.
[<RequireQualifiedAccess>]
type DeliveryTaskReference =
    | ChildWork of handle: string
    | Invocation of invocationId: string
    | Lifecycle of workId: string * incumbencyId: string

/// The host's attribution of one tool call: the task the call serves, the
/// logical run and the authority it belongs to, and the prose the model
/// wrote before the call. The settle point cannot derive any of it — it runs
/// below the host turn, where only the call fence is visible.
type DeliveryAttribution =
    { LogicalRunId: LogicalRunId
      AuthorityRoot: AuthorityRootUserMessageId
      PhysicalUserMessageId: PhysicalUserMessageId
      Task: DeliveryTaskReference
      RetirementId: string option
      FormalText: string }

/// The delivery witness of one settled call. The host writer maps it onto
/// the durable witness fact — adding the material digest — and commits it;
/// this layer never touches the journal itself.
type DeliveryWitness =
    {
        SessionId: SessionId
        LogicalRunId: LogicalRunId
        AuthorityRoot: AuthorityRootUserMessageId
        PhysicalUserMessageId: PhysicalUserMessageId
        ProviderRun: ProviderRunIdentity
        ToolCallId: ToolCallId
        Task: DeliveryTaskReference
        FormalText: string
        ViewDeclaration: string
        /// Canonical returned material; rendering belongs to the final wire boundary.
        View: string
        RetirementId: string option
    }

/// Required outcome shape of a durable delivery witness commit.
[<RequireQualifiedAccess>]
type DeliveryCommitResult =
    | Committed
    | NotCommitted of reason: string
    | Unknown of reason: string

/// The narrow durable capability the settle point uses to record a delivery
/// witness. A refused commit means the delivery is not established: the
/// caller must learn that instead of reading a success nobody can prove.
type IDeliveryWitnessWriter =
    abstract CommitDeliveryWitness: witness: DeliveryWitness -> Task<DeliveryCommitResult>

/// What the settle point needs to attribute a delivery and to make its
/// evidence durable. A call without a settlement has no durable store behind
/// it (test/dev harness): the process-live cell is then the only evidence.
///
/// A durable host whose call cannot be attributed is NOT the harness case:
/// `UnattributedDelivery` refuses the delivery because no witness can carry
/// it to the caller's join.
type DeliverySettlement =
    | DurableDelivery of DeliveryAttribution * IDeliveryWitnessWriter
    | UnattributedDelivery of reason: string
    | ContextUnavailable of reason: string
    | DeferredDelivery of resolve: (unit -> Task<DeliverySettlement option>)

type JsReviewGates =
    { ReadGate: (unit -> Task<bool>) option
      WriteGate: (unit -> bool) option }

module JsToolWorkflow =
    type FileAccessObservation = JsExplicitFileRead list -> string list -> Task<unit>

    [<RequireQualifiedAccess>]
    type JsToolOutcome =
        | Succeeded of
            value: obj *
            rewritten: string list *
            created: string list *
            removed: string list *
            moved: (string * string) list
        | Failed of
            failure: JsFailure *
            rewritten: string list *
            created: string list *
            removed: string list *
            moved: (string * string) list
        | Parked of name: string * payload: obj * source: string

    val run:
        capabilities: Set<JsCapability> ->
        root: string ->
        baseClassSource: string ->
        modelSource: string ->
        deadlineMs: int ->
        deadlineEpochMs: int64 ->
        outputBoundBytes: int ->
        persistence: IJsTransactionPersistence option ->
            Task<JsToolOutcome>

    val runWithFileAccessObservation:
        capabilities: Set<JsCapability> ->
        root: string ->
        baseClassSource: string ->
        modelSource: string ->
        deadlineMs: int ->
        deadlineEpochMs: int64 ->
        outputBoundBytes: int ->
        persistence: IJsTransactionPersistence option ->
        fileAccessObservation: FileAccessObservation ->
            Task<JsToolOutcome>

    val execute:
        sessionId: string ->
        fence: (ProviderRunIdentity * ToolCallId) option ->
        capabilities: Set<JsCapability> ->
        root: string ->
        baseClassSource: string ->
        input: obj ->
        deadlineMs: int ->
        deadlineEpochMs: int64 ->
        outputBoundBytes: int ->
        persistence: IJsTransactionPersistence option ->
        lifecyclePersistence: IJsLifecyclePersistence option ->
        fileAccessObservation: FileAccessObservation option ->
            Task<JsToolOutcome>

    val executeWithReviewCheck:
        reviewGates: JsReviewGates option ->
        sessionId: string ->
        fence: (ProviderRunIdentity * ToolCallId) option ->
        capabilities: Set<JsCapability> ->
        root: string ->
        baseClassSource: string ->
        input: obj ->
        deadlineMs: int ->
        deadlineEpochMs: int64 ->
        outputBoundBytes: int ->
        persistence: IJsTransactionPersistence option ->
        lifecyclePersistence: IJsLifecyclePersistence option ->
        fileAccessObservation: FileAccessObservation option ->
            Task<JsToolOutcome>

    val executeWithReviewCheckAndDelivery:
        reviewGates: JsReviewGates option ->
        settlement: DeliverySettlement option ->
        sessionId: string ->
        fence: (ProviderRunIdentity * ToolCallId) option ->
        capabilities: Set<JsCapability> ->
        root: string ->
        baseClassSource: string ->
        input: obj ->
        deadlineMs: int ->
        deadlineEpochMs: int64 ->
        outputBoundBytes: int ->
        persistence: IJsTransactionPersistence option ->
        lifecyclePersistence: IJsLifecyclePersistence option ->
        fileAccessObservation: FileAccessObservation option ->
            Task<JsToolOutcome>

    val conflictingInputReason: input: obj -> string option

module JsToolsResult =
    val render: outcome: JsToolWorkflow.JsToolOutcome -> string
