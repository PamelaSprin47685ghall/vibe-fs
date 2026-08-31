namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// SessionDeleted teardown: LoopSensor / Strength / SyncDelegate / Quiescence / Dispose.
/// Caller supplies `signalReconciler` so this module never owns the Scheduler.
module HostSessionDeletion =

    type SessionDeletionPreparation =
        private | SessionDeletionPreparation of
            parent: SessionId option *
            inspectorStaged: bool *
            inspectorToFinalize: SessionId option

    let private stageDeletedInspector
        (runtime: SyncDelegateRuntime)
        (sessionId: SessionId)
        (fallbackParent: SessionId option)
        : SessionId option * bool =
        match runtime.StageDeletedInspectorBySession sessionId with
        | Some ownerSessionId -> Some ownerSessionId, true
        | None ->
            let inspectorStaged =
                fallbackParent
                |> Option.exists (fun parentSessionId -> runtime.StageDeletedInspector(parentSessionId, sessionId))

            fallbackParent, inspectorStaged

    /// Capture parent topology and retire the live Inspector binding synchronously
    /// at Host event admission. Child and owner cleanup may await independently,
    /// but their semantic order is now fixed by the public event stream.
    let prepare
        (scope: PluginRuntimeScope)
        (sessionId: SessionId)
        (parentSessionIdOpt: SessionId option)
        : SessionDeletionPreparation =
        let parent =
            parentSessionIdOpt
            |> Option.orElseWith (fun () ->
                match scope.Sessions.SessionParents.TryGetValue(SessionId.value sessionId) with
                | true, parentId -> Some(SessionId.create parentId)
                | false, _ -> None)

        match scope.SyncDelegateRuntime with
        | None -> SessionDeletionPreparation(parent, false, None)
        | Some runtime ->
            let resolvedParent, inspectorStaged = stageDeletedInspector runtime sessionId parent

            let inspectorToFinalize =
                runtime.TryFindForScopeClose(sessionId, SyncDelegateRole.Inspector)

            SessionDeletionPreparation(resolvedParent, inspectorStaged, inspectorToFinalize)

    /// Finalize the retained Inspector case before later session cleanup drops its
    /// physical identity. Failure is diagnostic and process-fatal.
    let private finalizeInspectorAtRoot
        (finalizeInspector: string -> string -> Task<Result<unit, string>>)
        (root: string)
        (inspectorId: SessionId)
        : Task =
        task {
            match! finalizeInspector root (SessionId.value inspectorId) with
            | Ok() -> ()
            | Error error ->
                Diagnostic.fatal
                    "inspector-case-finalization-failed"
                    [ "session_id", SessionId.value inspectorId; "result", error ]

                return
                    invalidOp (
                        sprintf "CASE-003: Inspector %s finalization failed: %s" (SessionId.value inspectorId) error
                    )
        }

    let private finalizeInspectorIfRoot
        (workspaceDirectory: string option)
        (finalizeInspector: string -> string -> Task<Result<unit, string>>)
        (inspectorId: SessionId)
        : Task =
        match workspaceDirectory with
        | Some root -> finalizeInspectorAtRoot finalizeInspector root inspectorId
        | None -> Task.FromResult() :> Task

    let private finalizeRetainedInspector
        (scope: PluginRuntimeScope)
        (workspaceDirectory: string option)
        (finalizeInspector: string -> string -> Task<Result<unit, string>>)
        (inspectorId: SessionId)
        : Task =
        task {
            try
                do! finalizeInspectorIfRoot workspaceDirectory finalizeInspector inspectorId
            finally
                scope.DropSessionIdentity(SessionId.value inspectorId)
        }

    let finalizePreparedInspector
        (scope: PluginRuntimeScope)
        (workspaceDirectory: string option)
        (finalizeInspector: string -> string -> Task<Result<unit, string>>)
        (SessionDeletionPreparation(_, _, inspectorToFinalize))
        : Task =
        inspectorToFinalize
        |> Option.map (finalizeRetainedInspector scope workspaceDirectory finalizeInspector)
        |> Option.defaultValue (Task.FromResult() :> Task)

    let private cleanupRuntime
        (scope: PluginRuntimeScope)
        (runtimeOpt: SyncDelegateRuntime option)
        (cleanupInspectorDraft: string -> unit)
        (sessionId: SessionId)
        : Task =
        task {
            match runtimeOpt with
            | Some runtime -> runtime.CancelSession sessionId
            | None -> ()

            cleanupInspectorDraft (SessionId.value sessionId)
        }

    let handle
        (scope: PluginRuntimeScope)
        (cleanupInspectorDraft: string -> unit)
        (signalReconciler: HostSignal -> unit)
        (sessionId: SessionId)
        (SessionDeletionPreparation(parentSessionIdOpt, stagedInspector, _))
        : Task =
        scope.LoopSensor.DropSession sessionId

        // STRENGTH-004/011: owner deletion cancels the decision-local
        // InternalLeaf immediately. CancelOwner completes the waiting
        // decision before its best-effort physical abort, so no deleted
        // owner can keep a Replica eligible for later collection.
        scope.Strength.StrengthReplicaRuntime
        |> Option.iter (fun runtime ->
            runtime.CancelOwner sessionId |> ignore
            runtime.HandleSessionDeleted sessionId)

        // OpenCode recursively emits child SessionDeleted before the owner
        // SessionDeleted. An attached Inspector child must retire its live
        // binding without clearing the Casebook draft; the later owner
        // event is the graceful ReuseScope-close signal that finalizes it.
        // A continued owner Invoke consumes the staged child as unexpected
        // deletion and cleans its draft instead of reusing the dead child.
        let signal = SessionDeleted(sessionId, parentSessionIdOpt)

        task {
            if not stagedInspector then
                do! cleanupRuntime scope scope.SyncDelegateRuntime cleanupInspectorDraft sessionId

            scope.Sessions.Quiescence.DropSession sessionId
            ExplicitResumeSuppression.dropSession sessionId

            if stagedInspector then
                do! scope.DisposeSessionPreservingIdentity(SessionId.value sessionId)
            else
                do! scope.DisposeSession(SessionId.value sessionId)

            signalReconciler signal
        }
