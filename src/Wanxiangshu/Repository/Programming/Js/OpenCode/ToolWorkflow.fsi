namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Repository.Programming.Js

/// Parse the sandbox JSON string into LlmFacing reference data and enforce JS-010.
module JsToolsData =
    val parse: json: string -> Result<obj, JsFailure>
    val isDelivered: json: string -> bool
    val parseOutcome: json: string -> Result<obj, JsFailure>
    val parseParkedPayload: json: string -> obj

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
    { SessionId: SessionId
      LogicalRunId: LogicalRunId
      AuthorityRoot: AuthorityRootUserMessageId
      PhysicalUserMessageId: PhysicalUserMessageId
      ProviderRun: ProviderRunIdentity
      ToolCallId: ToolCallId
      Task: DeliveryTaskReference
      FormalText: string
      ViewDeclaration: string
      View: string
      RetirementId: string option }

/// The narrow durable capability the settle point uses to record a delivery
/// witness. A refused commit means the delivery is not established: the
/// caller must learn that instead of reading a success nobody can prove.
type IDeliveryWitnessWriter =
    abstract CommitDeliveryWitness: witness: DeliveryWitness -> Task<Result<unit, string>>

/// What the settle point needs to attribute a delivery and to make its
/// evidence durable. A call without a settlement has no durable store behind
/// it (test/dev harness): the process-live cell is then the only evidence.
type DeliverySettlement =
    { Attribution: DeliveryAttribution
      Writer: IDeliveryWitnessWriter }

/// JS-085: one js-* tool invocation, end to end — the only orchestration of
/// sandbox execution, as-is per-write persistence, durable accounting and the
/// commit fact (JS-012/JS-013/JS-015). With `store = None` the workflow is
/// ephemeral (no durable facts — tests); with a store, the written mutations
/// are accounted as Prepared/Committed after the effects already landed.
module JsToolWorkflow =
    type FileAccessObservation = JsExplicitFileRead list -> string list -> Task<unit>

    /// Outcome of one invocation: the program's structured value plus the
    /// commit report — or a stable JsFailure. `Parked` is the top-level parked
    /// payload: the program suspended at `await this.park(payload)` and the site
    /// is registered under `handle` (PLAN S2 step 4).
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
        | Parked of handle: string * payload: obj * source: string

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

    /// Execute one js-* tool invocation under the three-state input protocol
    /// (PLAN S2 step 5): `{ program }` runs a new program, `{ unpark: {
    /// handle, value } }` resumes a parked site, `{ abandon: { handle } }`
    /// enters the ABANDONED protocol (algorithm F). `sessionId` tags the
    /// parked site so session close can reap it.
    val execute:
        sessionId: string ->
        fence: (Wanxiangshu.Foundation.Identity.ProviderRunIdentity * Wanxiangshu.Foundation.Identity.ToolCallId) option ->
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

    /// Execute one js-* tool invocation whose delivery commits a durable
    /// delivery witness: the settlement carries the host's attribution of the
    /// call and the durable place its witness goes.
    val executeWithDelivery:
        settlement: DeliverySettlement option ->
        sessionId: string ->
        fence: (Wanxiangshu.Foundation.Identity.ProviderRunIdentity * Wanxiangshu.Foundation.Identity.ToolCallId) option ->
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
        reviewCheck: (unit -> Task<bool>) option ->
        sessionId: string ->
        fence: (Wanxiangshu.Foundation.Identity.ProviderRunIdentity * Wanxiangshu.Foundation.Identity.ToolCallId) option ->
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

    /// The review-checked invocation whose delivery commits a durable
    /// delivery witness.
    val executeWithReviewCheckAndDelivery:
        reviewCheck: (unit -> Task<bool>) option ->
        settlement: DeliverySettlement option ->
        sessionId: string ->
        fence: (Wanxiangshu.Foundation.Identity.ProviderRunIdentity * Wanxiangshu.Foundation.Identity.ToolCallId) option ->
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

    /// Run a program whose api is caller-supplied while the park lifecycle,
    /// race and outcome stay workflow-owned (the plan tenure sandbox).
    val runWithParkApi:
        sessionId: string ->
        baseClassSource: string ->
        modelSource: string ->
        makeApi: (Wanxiangshu.Process.JsSandbox.ParkState -> obj) ->
        deadlineMs: int ->
        deadlineEpochMs: int64 ->
        outputBoundBytes: int ->
            Task<JsToolOutcome>

    /// Why a js-* input is contradictory: it carries more than one of
    /// program / unpark / abandon. `None` when the input carries at most one
    /// state, so a caller can refuse a conflicting call before anything runs.
    val conflictingInputReason: input: obj -> string option

/// JS-016: stable LLM-visible result shapes, rendered as Synthetic TOML
/// (the only rendering owner; ARCH-010). Success is `# ok` plus data/fs;
/// failure is `# failed` plus code/reason; a parked payload renders as a
/// top-level `parked` table (PLAN S2 step 4).
module JsToolsResult =
    val render: outcome: JsToolWorkflow.JsToolOutcome -> string
