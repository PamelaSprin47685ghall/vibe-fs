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

    /// MANAGED-SESSION-001: drive SyncDelegateRuntime's production child
    /// observation into AttachedSessionRuntime against controlled Host callbacks.
    val managedChildReconciliationScenario: directory: string -> mode: string -> Task<obj>

    /// MANAGED-SESSION-001: two simultaneous callers for one exact key share
    /// the complete physical reconciliation transaction and its result.
    val concurrentAttachedGetOrCreateScenario: unit -> Task<obj>

    /// Execute the real InspectorTool specification against the opaque scope and
    /// SyncDelegate runtime. Tool arguments/context are translated here so the
    /// semantic caller never imports ToolHostCodec or InspectorTool internals.
    val executeInspector: value: obj -> toolModule: obj -> owner: string -> charge: string -> Task<string>

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
    val stageDeletedInspector: value: obj -> owner: string -> bool
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
    val retryDisposition: outcomes: string array -> obj
    val dispose: value: obj -> unit
