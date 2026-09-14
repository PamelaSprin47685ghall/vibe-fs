namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Repository.Knowledge.Casebook
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

    /// Finalize the staged Inspector case before later session cleanup drops its
    /// physical identity. The exact settlement is captured FIRST; only a durably
    /// settled finalize releases the identity (InspectorFinalizeSettlement.
    /// releasesIdentity). NotCommitted/Unknown/PhaseConflict RETAIN the identity
    /// so a later recovery can resume the exact finalize — the outer evidence
    /// lifetime decision below never drops an identity it still needs.
    let private finalizeInspectorAtRoot
        (finalizeInspector: string -> string -> Task<InspectorFinalizeSettlement>)
        (root: string)
        (inspectorId: SessionId)
        : Task<InspectorFinalizeSettlement> =
        finalizeInspector root (SessionId.value inspectorId)

    let private finalizeInspectorIfRoot
        (workspaceDirectory: string option)
        (finalizeInspector: string -> string -> Task<InspectorFinalizeSettlement>)
        (inspectorId: SessionId)
        : Task<InspectorFinalizeSettlement option> =
        match workspaceDirectory with
        | Some root ->
            task {
                let! settled = finalizeInspectorAtRoot finalizeInspector root inspectorId
                return Some settled
            }
        | None -> Task.FromResult None

    /// One settle dispatch at top level so the match inside
    /// `finalizeStagedInspector` stays a single pyramid level.
    let private finalizeDisposition
        (scope: PluginRuntimeScope)
        (inspectorId: SessionId)
        (settled: InspectorFinalizeSettlement)
        : unit =
        match settled.Commitment with
        | InspectorFinalizeCommitment.Finalized
        | InspectorFinalizeCommitment.NothingToFinalize ->
            scope.DropSessionIdentity(SessionId.value inspectorId)
        | InspectorFinalizeCommitment.NotCommitted reason
        | InspectorFinalizeCommitment.Unknown reason ->
            // Retain the identity: a later recovery must be able to
            // resume this exact finalize. Expected/best-effort, never a
            // recovery decision — the commitment itself is the evidence.
            Diagnostic.emit
                "inspector-case-finalization-pending"
                [ "session_id", SessionId.value inspectorId; "result", reason ]
        | InspectorFinalizeCommitment.PhaseConflict reason ->
            Diagnostic.fatal
                "inspector-case-finalization-failed"
                [ "session_id", SessionId.value inspectorId; "result", reason ]

            raise (invalidOp (sprintf "CASE-003: Inspector %s finalization conflict: %s" (SessionId.value inspectorId) reason))

    let private finalizeStagedInspector
        (scope: PluginRuntimeScope)
        (workspaceDirectory: string option)
        (finalizeInspector: string -> string -> Task<InspectorFinalizeSettlement>)
        (inspectorId: SessionId)
        : Task =
        task {
            // F35: capture the exact finalize evidence BEFORE deciding identity
            // lifetime. The identity drop below is explicit and owner-driven —
            // it runs only for a durably settled finalize, never in a finally
            // that would also erase the identity a failed finalize still needs.
            let! settlementOpt = finalizeInspectorIfRoot workspaceDirectory finalizeInspector inspectorId

            match settlementOpt with
            | None -> ()
            | Some settled -> finalizeDisposition scope inspectorId settled
        }

    let finalizePreparedInspector
        (scope: PluginRuntimeScope)
        (workspaceDirectory: string option)
        (finalizeInspector: string -> string -> Task<InspectorFinalizeSettlement>)
        (SessionDeletionPreparation(_, _, inspectorToFinalize))
        : Task =
        inspectorToFinalize
        |> Option.map (finalizeStagedInspector scope workspaceDirectory finalizeInspector)
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
        (onSessionDeleted: (SessionId -> unit) option)
        (SessionDeletionPreparation(parentSessionIdOpt, stagedInspector, _))
        : Task =
        scope.LoopSensor.DropSession sessionId

        // STRENGTH-004/011: owner deletion cancels the decision-local
        // InternalLeaf immediately. CancelOwner completes the waiting
        // decision before its best-effort physical abort, so no deleted
        // owner can keep a Replica eligible for later collection.
        onSessionDeleted |> Option.iter (fun onDeleted -> onDeleted sessionId)

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

            do! scope.DisposeSession(SessionId.value sessionId)

            signalReconciler signal
        }
