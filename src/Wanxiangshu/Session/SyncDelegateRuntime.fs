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

/// EXEC-026 / EXEC-028: reusable SyncDelegate CE (Acquire → GetOrCreate → Send →
/// await Returned → await Completion). Dual-await path for dedicated Inspector/Coder
/// (Work+Attached); not SatelliteRuntime / SatelliteKind.
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
        ?promptModel: OpencodeModel
    ) =
    let store = SyncDelegateCallStore()
    let recoveryBudget = AgentPairCursor.DefaultAutoRecoveryBudget
    let directory = workspaceDirectory
    let noteInspectorPrompt = defaultArg onInspectorPrompt (fun _ _ -> ())
    let noteInspectorAnswer = defaultArg onInspectorAnswer (fun _ _ -> ())
    let cleanupInspectorDraft = defaultArg onInspectorCleanup (fun _ -> ())
    let boundPromptModel = promptModel

    let sessionKey (sessionId: SessionId) = SessionId.value sessionId

    let scopeKey (scope: ReuseScopeId) = ReuseScopeId.value scope

    let roleLabel =
        function
        | SyncDelegateRole.Inspector -> "inspector"
        | SyncDelegateRole.Coder -> "coder"

    let canonicalRole =
        function
        | SyncDelegateRole.Inspector -> Role.Inspector
        | SyncDelegateRole.Coder -> Role.Coder

    let normalizePayload (text: string) = if isNull text then "" else text.Trim()

    // EXEC-028: dedicated SyncDelegate Inspector/Coder must expose `return` on the
    // per-request tools map (Returned → Completion). Static role permissions stay
    // unchanged for forked OneShot-style children; only this CE path adds Return.
    let toolMap role =
        PromptAuthority.toolCapabilitiesFor role ProviderRequestKind.WorkMain
        |> Set.add ToolPermission.Return
        |> StaticTools.requestToolMap

    let activeProfile sessionId = dispatcher.ActiveProfile sessionId

    let createChild (owner: SessionId) (agentName: string) (childDirectory: string option) =
        sessions.CreateChildSession(
            owner,
            { Title = Some agentName
              Agent = Some agentName
              Directory = childDirectory }
        )

    let sendDelegatePrompt (call: SyncDelegateCall) (message: string) =
        task {
            let tools = toolMap (canonicalRole call.Role)

            // Wave B: each Invoke is AgentOwnerRoot (Detached). SyncDelegate
            // ContinuationKind (question / idle) lands with tool cutover.
            return!
                dispatcher.SendAgentOwnerRootWithTools
                    sessions
                    call.Delegate
                    message
                    call.Agent
                    directory
                    PromptDispatcher.AwaitMode.Detached
                    None
                    tools
                    boundPromptModel
        }

    let sendIdleNudge (permit: QuiescencePermit option) (call: SyncDelegateCall) =
        task {
            match permit with
            | None -> return Error "Superseded: no idle permit for SyncDelegateIdleNudge"
            | Some current when not (quiescence.TryConsume current) ->
                return Error "Superseded: idle permit stale for SyncDelegateIdleNudge"
            | Some _ ->
                return!
                    dispatcher.SendAgentOwnerRootWithTools
                        sessions
                        call.Delegate
                        SyncDelegatePrompt.idleNudge
                        call.Agent
                        directory
                        PromptDispatcher.AwaitMode.Detached
                        None
                        (toolMap (canonicalRole call.Role))
                        boundPromptModel
        }

    let deps: SyncDelegateWorkflow.Dependencies =
        { Attached = attached
          ResolveOwnerTier = resolveOwnerTier
          CreateChild = createChild
          OnDelegateReady = onDelegateReady
          NoteInspectorPrompt = noteInspectorPrompt
          CleanupInspectorDraft = cleanupInspectorDraft
          Directory = directory
          // SendAgentOwnerRootWithTools yields a PromptKey the CE discards;
          // the workflow dependency contracts on the settle result only.
          SendPrompt =
            fun call message ->
                task {
                    let! result = sendDelegatePrompt call message
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
            match store.TryCallByDelegate inspectorSessionId with
            | Some call ->
                store.ClearPendingText inspectorSessionId
                store.RemoveCall call
                store.FailCall(call, "Sync delegate Inspector session was deleted")
                store.ReleaseFlight call.OwnerScope
            | None -> store.ClearPendingText inspectorSessionId

            attached.Remove(ownerSessionId, SyncDelegateRole.Inspector) |> ignore

            let ownerScope = ReuseScope.ofSession ownerSessionId

            let replaced = store.PutDeletedInspector(ownerScope, inspectorSessionId)

            replaced
            |> Option.filter (fun previous -> previous <> inspectorSessionId)
            |> Option.iter (fun previous -> cleanupInspectorDraft (sessionKey previous))

            true
        | _ -> false

    member _.Invoke(ownerSessionKey: string, role: SyncDelegateRole, message: string) : Task<Result<string, string>> =
        SyncDelegateWorkflow.invoke store deps ownerSessionKey role message

    member _.Return
        (delegateSessionKey: string, providerRunId: ProviderRunIdentity option, message: string)
        : Task<Result<string, string>> =
        task {
            let delegateSession = SessionId.create delegateSessionKey

            match activeProfile delegateSession, providerRunId, store.TryCallByDelegate delegateSession with
            | None, _, _ -> return Error "return rejected: no active SyncDelegate Authority Root"
            | Some profile, _, _ when profile.CanonicalRole <> Role.Inspector && profile.CanonicalRole <> Role.Coder ->
                return Error "return rejected: role is neither Inspector nor Coder delegate"
            | Some _, None, _ -> return Error "return rejected: Host provided no provider-run identity"
            | Some profile, Some _, None -> return Error "return rejected: SyncDelegate has no active caller"
            | Some profile, Some _, Some call when call.Delegate <> delegateSession ->
                return Error "return rejected: delegate does not own the active sync call"
            | Some profile, Some toolRun, Some call when canonicalRole call.Role <> profile.CanonicalRole ->
                return Error "return rejected: active profile role does not match SyncDelegate binding"
            | Some _, Some toolRun, Some call ->
                if store.TryPendingText delegateSession |> Option.isSome then
                    return Error "return rejected: SyncDelegate return completion is already pending"
                else
                    store.ArmPendingText(
                        delegateSession,
                        { Text = SyncDelegatePrompt.SyncDelegateReturnCompletion
                          ToolRun = toolRun }
                    )

                    AsyncSupport.trySetResult call.Returned (Ok { Answer = message; ToolRun = toolRun })
                    |> ignore

                    if call.Role = SyncDelegateRole.Inspector then
                        noteInspectorAnswer delegateSessionKey message

                    return Ok SyncDelegatePrompt.returnResult
        }

    member _.TextComplete(input: obj, output: obj) =
        if
            not (isNull input)
            && not (isNull input?sessionID)
            && not (isNull input?messageID)
        then
            let sessionId = SessionId.create (unbox<string> input?sessionID)
            let completionRun = ProviderRunIdentity.create (unbox<string> input?messageID)

            match store.TryPendingText sessionId with
            | Some pending when completionRun <> pending.ToolRun -> output?text <- pending.Text
            | _ -> ()

    member _.HandleTurn(turn: ReconciledTurn, permit: QuiescencePermit option) : Task<bool> =
        task {
            match turn.Role, store.TryCallByDelegate turn.SessionId with
            | Some(Role.Inspector | Role.Coder), Some call ->
                match turn.Outcome with
                | ReconcileProgram.TurnCompleted ->
                    let payload = normalizePayload (CompletedTurnClassifier.partsText turn.Parts)

                    if payload = normalizePayload SyncDelegatePrompt.SyncDelegateReturnCompletion then
                        store.ClearPendingText turn.SessionId
                        store.RemoveCall call
                        AsyncSupport.trySetResult call.Completion (Ok()) |> ignore
                        return true
                    else
                        match call.Nudges >= recoveryBudget with
                        | true ->
                            store.ClearPendingText turn.SessionId
                            store.RemoveCall call

                            store.FailCall(
                                call,
                                (sprintf "SyncDelegate idle recovery budget exhausted after %i nudges" recoveryBudget)
                            )

                            return true
                        | false ->
                            match! sendIdleNudge permit call with
                            | Ok _ ->
                                store.UpdateCall(
                                    call.OwnerScope,
                                    (fun current ->
                                        { current with
                                            Nudges = current.Nudges + 1 })
                                )
                                |> ignore
                            | Error _ -> ()

                            return true
                | ReconcileProgram.TurnFailed error
                | ReconcileProgram.TurnAborted error ->
                    store.ClearPendingText turn.SessionId
                    store.RemoveCall call
                    store.FailCall(call, (sprintf "SyncDelegate run failed: %s" error))
                    return true
                | _ -> return true
            | _ -> return false
        }

    member _.CancelSession(sessionId: SessionId) : unit =
        let asOwnerScope = ReuseScope.ofSession sessionId

        let call =
            store.TryCallByOwnerScope asOwnerScope
            |> Option.orElseWith (fun () -> store.TryCallByDelegate sessionId)

        // Capture inspector draft holders before bindings are torn down.
        let inspectorOwned = attached.TryFind(sessionId, SyncDelegateRole.Inspector)

        let stagedInspectorOwned = store.ClearDeletedInspector asOwnerScope

        let inspectorAsDelegate =
            match call with
            | Some c when c.Role = SyncDelegateRole.Inspector -> Some c.Delegate
            | _ -> None

        call
        |> Option.iter (fun scope ->
            store.ClearPendingText scope.Delegate
            store.RemoveCall scope
            store.FailCall(scope, "Sync delegate call was cancelled")
            store.ReleaseFlight scope.OwnerScope)

        store.ClearPendingText sessionId
        attached.RemoveByDelegateSession sessionId |> ignore

        for role in [ SyncDelegateRole.Inspector; SyncDelegateRole.Coder ] do
            attached.Remove(sessionId, role) |> ignore

        inspectorOwned |> Option.iter (fun id -> cleanupInspectorDraft (sessionKey id))

        stagedInspectorOwned
        |> Option.iter (fun id -> cleanupInspectorDraft (sessionKey id))

        inspectorAsDelegate
        |> Option.iter (fun id -> cleanupInspectorDraft (sessionKey id))

        // Deleted id may itself hold an inspector draft (no owner binding left).
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
