namespace Wanxiangshu.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Tools

/// EXEC-026 / EXEC-031: reusable SyncDelegate CE (Acquire → GetOrCreate → Send →
/// ordinary Completion → bounded WorkRecord). No return tool / dual-await.
///
/// Composition seam: call state lives in SyncDelegateCallStore, the Invoke CE
/// lives in SyncDelegateWorkflow, wait descriptors in SyncDelegateWait.
type SyncDelegateRuntime
    (
        sessions: ISessionHostPort,
        dispatcher: PromptDispatcher.Runtime,
        journal: AgentJournal,
        attached: AttachedSessionRuntime,
        resolveOwnerTier: SessionId -> AgentTier option,
        onDelegateReady: SessionId -> string -> unit,
        quiescence: SessionQuiescenceGate,
        ?workspaceDirectory: string,
        /// Casebook draft hooks (wired from SpikePlugin → CasebookLifecycle; compile-order seam).
        ?onInspectorPrompt: string -> string -> unit,
        ?onInspectorAnswer: string -> string -> unit,
        ?onInspectorCleanup: string -> unit,
        /// G2 PREFIX LAW: bind the same ModelId on every Inspector/Coder child
        /// SendPrompt. ChatParamsHook leaves Model=None; Host agent config is not
        /// visible here, so the caller supplies the bound OpencodeModel.
        ?promptModel: OpencodeModel,
        /// EXEC-031: bounded WorkRecord projector (includeOpening=false). Injected
        /// from plugin wiring so Session does not depend on Finality compile order.
        ?workRecordFor: SessionId -> string option
    ) =
    let store = SyncDelegateCallStore()
    let directory = workspaceDirectory
    let noteInspectorPrompt = defaultArg onInspectorPrompt (fun _ _ -> ())
    let noteInspectorAnswer = defaultArg onInspectorAnswer (fun _ _ -> ())
    let cleanupInspectorDraft = defaultArg onInspectorCleanup (fun _ -> ())
    let boundPromptModel = promptModel
    let projectWorkRecord = defaultArg workRecordFor (fun _ -> None)

    let sessionKey (sessionId: SessionId) = SessionId.value sessionId

    let scopeKey (scope: ReuseScopeId) = ReuseScopeId.value scope

    let canonicalRole =
        function
        | SyncDelegateRole.Inspector -> Role.Inspector
        | SyncDelegateRole.Coder -> Role.Coder

    let normalizePayload (text: string) = if isNull text then "" else text.Trim()

    // EXEC-031: SyncDelegate uses ordinary WorkMain tools — no Return permission.
    let toolMap role =
        PromptAuthority.toolCapabilitiesFor role ProviderRequestKind.WorkMain
        |> StaticTools.requestToolMap

    let createChild (owner: SessionId) (agentName: string) (childDirectory: string option) =
        sessions.CreateChildSession(
            owner,
            { Title = Some agentName
              Agent = Some agentName
              Directory = childDirectory }
        )

    let sendDelegatePrompt (call: SyncDelegateCall) (request: SyncDelegatePromptRequest) =
        task {
            let tools = toolMap (canonicalRole call.Role)

            return!
                dispatcher.SendAgentOwnerRootWithTools
                    sessions
                    call.Delegate
                    request.ProviderPrompt
                    call.Agent
                    directory
                    PromptDispatcher.AwaitMode.Detached
                    None
                    tools
                    boundPromptModel
        }

    let materializeWorkRecord (delegateSession: SessionId) (fallback: string) =
        match projectWorkRecord delegateSession with
        | Some record when not (String.IsNullOrWhiteSpace record) -> record
        | _ -> fallback

    let deps: SyncDelegateWorkflow.Dependencies =
        { Attached = attached
          ResolveOwnerTier = resolveOwnerTier
          CreateChild = createChild
          OnDelegateReady = onDelegateReady
          NoteInspectorPrompt = noteInspectorPrompt
          CleanupInspectorDraft = cleanupInspectorDraft
          Directory = directory
          SendPrompt =
            fun call request ->
                task {
                    let! result = sendDelegatePrompt call request
                    return result |> Result.map ignore
                }
          DescribeWait = SyncDelegateWait.describe }

    member _.Attached = attached

    member _.TryFind(ownerSessionId: SessionId, role: SyncDelegateRole) = attached.TryFind(ownerSessionId, role)

    member _.TryFindForScopeClose(ownerSessionId: SessionId, role: SyncDelegateRole) =
        match attached.TryFind(ownerSessionId, role) with
        | Some sessionId -> Some sessionId
        | None when role = SyncDelegateRole.Inspector ->
            let ownerScope = ReuseScope.ofSession ownerSessionId
            store.TryGetDeletedInspector ownerScope
        | None -> None

    member _.StageDeletedInspector(ownerSessionId: SessionId, inspectorSessionId: SessionId) : bool =
        match attached.TryFind(ownerSessionId, SyncDelegateRole.Inspector) with
        | Some bound when bound = inspectorSessionId ->
            let rec popAll () =
                match store.TryPopCallByDelegate inspectorSessionId with
                | Some call ->
                    store.FailCall(call, "Sync delegate Inspector session was deleted")
                    popAll ()
                | None -> ()

            popAll ()

            attached.Remove(ownerSessionId, SyncDelegateRole.Inspector) |> ignore

            let ownerScope = ReuseScope.ofSession ownerSessionId

            let replaced = store.PutDeletedInspector(ownerScope, inspectorSessionId)

            replaced
            |> Option.filter (fun previous -> previous <> inspectorSessionId)
            |> Option.iter (fun previous -> cleanupInspectorDraft (sessionKey previous))

            true
        | _ -> false

    member _.Invoke(ownerSessionKey: string, role: SyncDelegateRole, charge: string) : Task<Result<string, string>> =
        SyncDelegateWorkflow.invoke store deps ownerSessionKey role charge (fun () -> Task.FromResult charge)

    /// EXEC-032 composition seam: caller supplies a low-trust provider prompt
    /// producer; workflow invokes it only after single-flight admission.
    member _.InvokePrepared
        (ownerSessionKey: string, role: SyncDelegateRole, charge: string, prepareProviderPrompt: unit -> Task<string>) : Task<
                                                                                                                             Result<
                                                                                                                                 string,
                                                                                                                                 string
                                                                                                                              >
                                                                                                                          >
        =
        SyncDelegateWorkflow.invoke store deps ownerSessionKey role charge prepareProviderPrompt

    member _.HandleTurn(turn: ReconciledTurn, permit: QuiescencePermit option) : Task<bool> =
        task {
            match turn.Role with
            | Some(Role.Inspector | Role.Coder) ->
                match store.TryPopCallByDelegate turn.SessionId with
                | Some call ->
                    match turn.Outcome with
                    | ReconcileProgram.TurnCompleted ->
                        let payload = normalizePayload (CompletedTurnClassifier.partsText turn.Parts)
                        let workRecord = materializeWorkRecord turn.SessionId payload

                        if call.Role = SyncDelegateRole.Inspector then
                            noteInspectorAnswer (sessionKey turn.SessionId) workRecord

                        AsyncSupport.trySetResult call.Answer (Ok workRecord) |> ignore
                        for inv in call.Invocations do
                            AsyncSupport.trySetResult inv.Completion (Ok workRecord) |> ignore
                        return true
                    | ReconcileProgram.TurnFailed error
                    | ReconcileProgram.TurnAborted error ->
                        store.FailCall(call, (sprintf "SyncDelegate run failed: %s" error))
                        return true
                    | _ -> return true
                | None -> return false
            | _ -> return false
        }

    member _.CancelSession(sessionId: SessionId) : unit =
        let asOwnerScope = ReuseScope.ofSession sessionId

        store.CancelScope asOwnerScope

        let rec popAll () =
            match store.TryPopCallByDelegate sessionId with
            | Some call ->
                store.FailCall(call, "Sync delegate call was cancelled")
                popAll ()
            | None -> ()

        popAll ()

        let inspectorOwned = attached.TryFind(sessionId, SyncDelegateRole.Inspector)

        let stagedInspectorOwned = store.ClearDeletedInspector asOwnerScope

        attached.RemoveByDelegateSession sessionId |> ignore

        for role in [ SyncDelegateRole.Inspector; SyncDelegateRole.Coder ] do
            attached.Remove(sessionId, role) |> ignore

        inspectorOwned |> Option.iter (fun id -> cleanupInspectorDraft (sessionKey id))

        stagedInspectorOwned
        |> Option.iter (fun id -> cleanupInspectorDraft (sessionKey id))

        cleanupInspectorDraft (sessionKey sessionId)

    member _.Dispose() =
        let retiredInspectors = store.ClearAll()

        for inspectorId in retiredInspectors do
            cleanupInspectorDraft (sessionKey inspectorId)

        attached.Clear()

    interface IDisposable with
        member runtime.Dispose() = runtime.Dispose()

/// Helpers for constructing `resolveOwnerTier` from journal / active profile —
/// SelectedTier source for any Work role.
module SyncDelegateTier =

    /// Prefer ActiveLogicalRun SelectedTier; fall back to LastAuthorityProfile.
    let fromDispatcher (dispatcher: PromptDispatcher.Runtime) (sessionId: SessionId) : AgentTier option =
        match dispatcher.ActiveProfile sessionId with
        | Some profile -> Some profile.SelectedTier
        | None ->
            (dispatcher.ProjectionFor sessionId).LastAuthorityProfile
            |> Option.map (fun profile -> profile.SelectedTier)

    /// Same resolution via AgentProjection snapshot (no dispatcher required).
    let fromJournal (journal: AgentJournal) (sessionId: SessionId) : AgentTier option =
        let projections = (AgentJournal.snapshot journal).AgentProjections

        PromptAuthorityLedger.activeProfile sessionId projections
        |> Option.orElseWith (fun () -> PromptAuthorityLedger.lastAuthorityProfile sessionId projections)
        |> Option.map (fun profile -> profile.SelectedTier)
