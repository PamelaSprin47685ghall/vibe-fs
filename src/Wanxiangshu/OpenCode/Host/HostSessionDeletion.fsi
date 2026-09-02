namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// SessionDeleted teardown: LoopSensor / Strength / SyncDelegate / Quiescence / Dispose.
/// Caller supplies `signalReconciler` so this module never owns the Scheduler.
module HostSessionDeletion =

    type SessionDeletionPreparation =
        private | SessionDeletionPreparation of
            parent: SessionId option *
            inspectorStaged: bool *
            inspectorToFinalize: SessionId option

    /// Capture parent topology and retire the live Inspector binding synchronously
    /// at Host event admission. Child and owner cleanup may await independently,
    /// but their semantic order is now fixed by the public event stream.
    val prepare:
        scope: PluginRuntimeScope ->
        sessionId: SessionId ->
        parentSessionIdOpt: SessionId option ->
            SessionDeletionPreparation

    val finalizePreparedInspector:
        scope: PluginRuntimeScope ->
        workspaceDirectory: string option ->
        finalizeInspector: (string -> string -> Task<Result<unit, string>>) ->
        preparation: SessionDeletionPreparation ->
            Task

    val handle:
        scope: PluginRuntimeScope ->
        cleanupInspectorDraft: (string -> unit) ->
        signalReconciler: (HostSignal -> unit) ->
        sessionId: SessionId ->
        preparation: SessionDeletionPreparation ->
            Task
