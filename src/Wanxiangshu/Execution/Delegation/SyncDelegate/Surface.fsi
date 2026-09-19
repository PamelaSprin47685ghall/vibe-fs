namespace Wanxiangshu.Execution.Delegation.SyncDelegate

open System.Threading.Tasks

/// Delegation-owned opaque runtime harness. Host sessions, journal writers,
/// attached-session state and completion turns never cross into JS; callers
/// observe only invocation promises and child identities.
[<RequireQualifiedAccess>]
module SyncDelegateSurface =
    /// Create a real SyncDelegateRuntime with an opaque journal and Host port.
    /// Every owner must first be admitted as an explicit durable HumanRoot.
    val create: directory: string -> owners: obj -> Task<obj>

    /// managed-session-lifecycle-001: drive SyncDelegateRuntime's production child
    /// observation into AttachedSessionRuntime against controlled Host callbacks.
    val managedChildReconciliationScenario: directory: string -> mode: string -> Task<obj>

    /// managed-session-lifecycle-001: two simultaneous callers for one exact key share
    /// the complete physical reconciliation transaction and its result.
    val concurrentAttachedGetOrCreateScenario: unit -> Task<obj>

    /// Run one internal Engineer research charge through the SyncDelegate
    /// runtime; the charge stays data and never becomes a tool-module surface.
    val executeEngineerCharge: value: obj -> owner: string -> charge: string -> Task<string>

    /// Invoke one ordinary managed delegation. The returned promise remains
    /// pending until `settle` receives a reconciled provider turn.
    val invoke: value: obj -> owner: string -> role: string -> question: string -> Task<obj>

    /// Settle the current managed child through the real HandleTurn path.
    val settleWithAuthorityRoot:
        value: obj ->
        owner: string ->
        role: string ->
        answer: string ->
        runId: string ->
        authorityRoot: string ->
            Task<bool>

    val settle: value: obj -> owner: string -> role: string -> answer: string -> runId: string -> Task<bool>

    val failWithAuthorityRoot:
        value: obj -> owner: string -> role: string -> reason: string -> authorityRoot: string -> Task<string>

    val observeTurn:
        value: obj ->
        owner: string ->
        role: string ->
        outcomeName: string ->
        answer: string ->
        runId: string ->
            Task<bool>

    val child: value: obj -> owner: string -> role: string -> obj
    val stageDeletedDelegate: value: obj -> owner: string -> bool
    val scopeCloseChild: value: obj -> owner: string -> role: string -> obj
    val cancelSession: value: obj -> session: string -> unit
    val vocabulary: roleName: string -> tierName: string -> scope: string -> obj
    val childCount: value: obj -> int
    val promptCount: value: obj -> owner: string -> role: string -> int
    val awaitPromptCount: value: obj -> owner: string -> role: string -> count: int -> Task
    val acceptPrompt: value: obj -> owner: string -> role: string -> index: int -> bool
    val promptOrigin: value: obj -> owner: string -> role: string -> index: int -> obj
    val prompt: value: obj -> owner: string -> role: string -> index: int -> obj
    val captureOwnerOpening: value: obj -> owner: string -> text: string -> Task
    val captureOwnerDeltaPart: value: obj -> owner: string -> text: string -> providerRun: string -> Task
    val handoffFrontier: value: obj -> owner: string -> role: string -> obj
    val batchOrder: roleName: string -> toolNames: string array -> currentCall: string -> obj

    val invokeBatch:
        value: obj ->
        owner: string ->
        role: string ->
        charge: string ->
        providerRun: string ->
        callId: string ->
        callOrder: string array ->
            Task<obj>

    val serializationDecision: firstScope: string -> secondScope: string -> sameProviderRun: bool -> obj
    val evidenceBoundary: charge: string -> workRecord: string -> obj

    /// Script the retry decorator's verdicts for the next confirmed failures:
    /// `"dispatched"`, `"superseded"` or `"terminal:<reason>"`.
    val scriptRetry: value: obj -> verdicts: string array -> unit

    /// How many times the decorated path asked the retry decorator for a verdict.
    val retryCalls: value: obj -> int

    /// Model the Host accepting the retry attempt the decorator dispatched.
    val dispatchRetryAttempt: value: obj -> owner: string -> role: string -> bool
    val dispose: value: obj -> unit

    /// delegation-031 probe: close the journal writer so later appends are known
    /// NotAttempted; the next invocation must still deliver its WorkRecord.
    val closeJournalWriter: value: obj -> unit

    /// delegation-031 probe: run the production checkpoint for one prepared handoff
    /// and return the boxed settlement it reports.
    val checkpointForHarness: value: obj -> owner: string -> role: string -> parentEndExclusive: int -> Task<obj>

    /// delegation-031 probe: abandon the pending call for this delegate (parent
    /// supersede guard) so a stale completion afterwards cannot claim it.
    val abandonPendingCall: value: obj -> owner: string -> role: string -> bool

    val stageDeferredInspection:
        sessionId: string -> callId: string -> charge: string -> keywords: string -> estimate: int option -> string

    val applyReplacedResults: messages: obj list -> obj list
