namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// SessionDeleted teardown: LoopSensor / Strength / SyncDelegate / Quiescence / Dispose.
/// Caller supplies `signalReconciler` so this module never owns the Scheduler.
module HostSessionDeletion =

    /// Sync prefix (DropSession + CancelOwner) runs before the returned Task starts
    /// awaiting CaseFinalize. Bootstrap fire-and-forgets the Task via emitJsExpr.
    let handle
        (scope: PluginRuntimeScope)
        (workspaceDirectory: string option)
        (finalizeInspector: string -> string -> Task<Result<unit, string>>)
        (cleanupInspectorDraft: string -> unit)
        (signalReconciler: HostSignal -> unit)
        (sessionId: SessionId)
        (parentSessionIdOpt: SessionId option)
        : Task =
        scope.LoopSensor.DropSession sessionId
        let assistanceDrop = scope.DropAssistanceSession sessionId

        // STRENGTH-004/011: owner deletion cancels the decision-local
        // InternalLeaf immediately. CancelOwner completes the waiting
        // decision before its best-effort physical abort, so no deleted
        // owner can keep a Replica eligible for later collection.
        scope.Strength.StrengthReplicaRuntime
        |> Option.iter (fun runtime -> runtime.CancelOwner sessionId |> ignore)

        // OpenCode recursively emits child SessionDeleted before the owner
        // SessionDeleted. An attached Inspector child must retire its live
        // binding without clearing the Casebook draft; the later owner
        // event is the graceful ReuseScope-close signal that finalizes it.
        // A continued owner Invoke consumes the staged child as unexpected
        // deletion and cleans its draft instead of reusing the dead child.
        let signal = SessionDeleted(sessionId, parentSessionIdOpt)

        task {
            // DELEG-018: physical assistance abort was issued synchronously above;
            // durable HandleAbandoned must settle before session/journal teardown.
            do! assistanceDrop

            let stagedInspector =
                match scope.SyncDelegateRuntime, parentSessionIdOpt with
                | Some runtime, Some parentSessionId -> runtime.StageDeletedInspector(parentSessionId, sessionId)
                | _ -> false

            if not stagedInspector then
                match scope.SyncDelegateRuntime with
                | Some runtime ->
                    match runtime.TryFindForScopeClose(sessionId, SyncDelegateRole.Inspector) with
                    | Some inspectorId ->
                        match workspaceDirectory with
                        | Some root ->
                            let! _ = finalizeInspector root (SessionId.value inspectorId)
                            ()
                        | None -> ()
                    | None -> ()

                    runtime.CancelSession sessionId
                | None -> ()

                // Residual draft if the deleted id itself held Inspector Q/A.
                cleanupInspectorDraft (SessionId.value sessionId)

            scope.Sessions.Quiescence.DropSession sessionId
            scope.DisposeSession(SessionId.value sessionId)
            signalReconciler signal
        }
