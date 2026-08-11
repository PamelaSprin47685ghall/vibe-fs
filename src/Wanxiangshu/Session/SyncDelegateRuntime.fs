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

type private SyncDelegateAnswer =
    { Answer: string
      ToolRun: ProviderRunIdentity }

/// In-flight sync delegate call: dual CE await points (Returned, Completion).
type private SyncDelegateCall =
    { Owner: SessionId
      OwnerScope: ReuseScopeId
      Role: SyncDelegateRole
      Delegate: SessionId
      Agent: string
      Returned: TaskCompletionSource<Result<SyncDelegateAnswer, string>>
      Completion: TaskCompletionSource<Result<unit, string>>
      Nudges: int }

/// TextComplete rewrite arm only — presence must not select HandleTurn branches.
type private PendingSyncCompletionText =
    { Text: string
      ToolRun: ProviderRunIdentity }

type private SyncDelegateWait =
    | ReturnFromDelegate of owner: SessionId * delegateSession: SessionId * role: SyncDelegateRole
    | DelegateCompletionTerminal of
        owner: SessionId *
        delegateSession: SessionId *
        role: SyncDelegateRole *
        toolRun: ProviderRunIdentity

/// EXEC-026 / EXEC-028: reusable SyncDelegate CE (Acquire → GetOrCreate → Send →
/// await Returned → await Completion). Dual-await path for dedicated Inspector/Coder
/// (Work+Attached); not SatelliteRuntime / SatelliteKind.
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
    let gate = obj ()
    let callsByOwnerScope = Dictionary<string, SyncDelegateCall>()
    let callsByDelegate = Dictionary<string, SyncDelegateCall>()
    let pendingCompletionTexts = Dictionary<string, PendingSyncCompletionText>()
    let inFlightScopes = HashSet<string>()
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

    let describeWait (wait: SyncDelegateWait) : DiagnosticWait =
        let toolOwner (owner: SessionId) =
            CausalOwner.create "sync-delegate-tool" [ "session", SessionId.value owner ]

        let delegateProducer (delegateSession: SessionId) =
            WorkflowProducer(
                CausalOwner.create "sync-delegate-session-workflow" [ "session", SessionId.value delegateSession ]
            )

        let cancelEscape (owner: SessionId) =
            WaitEscape.CancelledBy(CausalOwner.create "owner-session" [ "session", SessionId.value owner ])

        match wait with
        | ReturnFromDelegate(owner, delegateSession, role) ->
            DiagnosticWait.create
                "sync-delegate-return"
                (toolOwner owner)
                [ "owner", SessionId.value owner
                  "delegate", SessionId.value delegateSession
                  "role", roleLabel role ]
                (delegateProducer delegateSession)
                [ cancelEscape owner; WaitEscape.SessionLifetime ]
                "SyncDelegateRuntime.Invoke"

        | DelegateCompletionTerminal(owner, delegateSession, role, toolRun) ->
            DiagnosticWait.create
                "sync-delegate-completion"
                (toolOwner owner)
                [ "owner", SessionId.value owner
                  "delegate", SessionId.value delegateSession
                  "role", roleLabel role
                  "tool_run", ProviderRunIdentity.value toolRun ]
                (delegateProducer delegateSession)
                [ cancelEscape owner; WaitEscape.SessionLifetime ]
                "SyncDelegateRuntime.Invoke"

    let tryCallByOwnerScope (scope: ReuseScopeId) =
        lock gate (fun () ->
            match callsByOwnerScope.TryGetValue(scopeKey scope) with
            | true, call -> Some call
            | false, _ -> None)

    let tryCallByDelegate (delegateSession: SessionId) =
        lock gate (fun () ->
            match callsByDelegate.TryGetValue(sessionKey delegateSession) with
            | true, call -> Some call
            | false, _ -> None)

    let tryPendingText sessionId =
        lock gate (fun () ->
            match pendingCompletionTexts.TryGetValue(sessionKey sessionId) with
            | true, pending -> Some pending
            | false, _ -> None)

    let armPendingText sessionId pending =
        lock gate (fun () -> pendingCompletionTexts.[sessionKey sessionId] <- pending)

    let clearPendingText sessionId =
        lock gate (fun () -> pendingCompletionTexts.Remove(sessionKey sessionId) |> ignore)

    let failCall (call: SyncDelegateCall) (error: string) =
        AsyncSupport.trySetResult call.Returned (Error error) |> ignore
        AsyncSupport.trySetResult call.Completion (Error error) |> ignore

    let removeCall (call: SyncDelegateCall) =
        lock gate (fun () ->
            callsByOwnerScope.Remove(scopeKey call.OwnerScope) |> ignore
            callsByDelegate.Remove(sessionKey call.Delegate) |> ignore)

    let updateCall ownerScope (update: SyncDelegateCall -> SyncDelegateCall) =
        lock gate (fun () ->
            let key = scopeKey ownerScope

            match callsByOwnerScope.TryGetValue key with
            | true, current ->
                let next = update current
                callsByOwnerScope.[key] <- next
                callsByDelegate.[sessionKey next.Delegate] <- next
                Some next
            | false, _ -> None)

    let releaseFlight (scope: ReuseScopeId) =
        lock gate (fun () -> inFlightScopes.Remove(scopeKey scope) |> ignore)

    let beginCall
        (owner: SessionId)
        (ownerScope: ReuseScopeId)
        (role: SyncDelegateRole)
        (delegateSession: SessionId)
        (agent: string)
        : SyncDelegateCall * IDisposable =
        let returned =
            TaskCompletionSource<Result<SyncDelegateAnswer, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

        let completion =
            TaskCompletionSource<Result<unit, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

        let call =
            { Owner = owner
              OwnerScope = ownerScope
              Role = role
              Delegate = delegateSession
              Agent = agent
              Returned = returned
              Completion = completion
              Nudges = 0 }

        let ownerKey = scopeKey ownerScope
        let delegateKey = sessionKey delegateSession

        lock gate (fun () ->
            callsByOwnerScope.[ownerKey] <- call
            callsByDelegate.[delegateKey] <- call)

        let registration =
            { new IDisposable with
                member _.Dispose() =
                    let stillOwned =
                        lock gate (fun () ->
                            match callsByOwnerScope.TryGetValue ownerKey with
                            | true, current when Object.ReferenceEquals(current.Returned, call.Returned) ->
                                callsByOwnerScope.Remove ownerKey |> ignore
                                callsByDelegate.Remove delegateKey |> ignore
                                true
                            | _ -> false)

                    if stillOwned then
                        failCall call "Sync delegate call scope disposed" }

        call, registration

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
                    ?model = boundPromptModel
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
                        ?model = boundPromptModel
        }

    member _.Attached = attached

    member _.TryFind(ownerSessionId: SessionId, role: SyncDelegateRole) = attached.TryFind(ownerSessionId, role)

    member _.Invoke(ownerSessionKey: string, role: SyncDelegateRole, message: string) : Task<Result<string, string>> =
        task {
            let owner = SessionId.create ownerSessionKey
            let ownerScope = ReuseScope.ofSession owner

            match resolveOwnerTier owner with
            | None -> return Error "sync delegate rejected: owner tier unknown"
            | Some ownerTier ->
                let claimed =
                    lock gate (fun () ->
                        let key = scopeKey ownerScope

                        if inFlightScopes.Contains key then
                            false
                        else
                            inFlightScopes.Add key |> ignore
                            true)

                if not claimed then
                    return Error "sync delegate rejected: another sync delegate call is in flight"
                else
                    let tier = SyncDelegate.tierForOwner ownerTier
                    let agentName = SyncDelegate.agentNameFor role tier

                    try
                        match!
                            attached.GetOrCreate(owner, role, agentName, directory, createChild, onDelegateReady)
                        with
                        | Error error ->
                            releaseFlight ownerScope
                            return Error error
                        | Ok delegateSession ->
                            let call, registration = beginCall owner ownerScope role delegateSession agentName

                            use _registration = registration

                            match! sendDelegatePrompt call message with
                            | Error error ->
                                failCall call error
                                removeCall call
                                releaseFlight ownerScope
                                return Error error
                            | Ok _ ->
                                if role = SyncDelegateRole.Inspector then
                                    noteInspectorPrompt (sessionKey delegateSession) message

                                let! returned =
                                    CausalAwait.awaitTask
                                        CausalWaitHub.observer
                                        (describeWait (ReturnFromDelegate(owner, delegateSession, role)))
                                        call.Returned.Task

                                match returned with
                                | Error error ->
                                    releaseFlight ownerScope
                                    return Error error
                                | Ok answer ->
                                    let! confirmed =
                                        CausalAwait.awaitTask
                                            CausalWaitHub.observer
                                            (describeWait (
                                                DelegateCompletionTerminal(owner, delegateSession, role, answer.ToolRun)
                                            ))
                                            call.Completion.Task

                                    releaseFlight ownerScope

                                    return confirmed |> Result.map (fun () -> answer.Answer)
                    with ex ->
                        releaseFlight ownerScope
                        return Error ex.Message
        }

    member _.Return
        (delegateSessionKey: string, providerRunId: ProviderRunIdentity option, message: string)
        : Task<Result<string, string>> =
        task {
            let delegateSession = SessionId.create delegateSessionKey

            match activeProfile delegateSession, providerRunId, tryCallByDelegate delegateSession with
            | None, _, _ -> return Error "return rejected: no active SyncDelegate Authority Root"
            | Some profile, _, _ when profile.CanonicalRole <> Role.Inspector && profile.CanonicalRole <> Role.Coder ->
                return Error "return rejected: role is neither Inspector nor Coder delegate"
            | Some _, None, _ -> return Error "return rejected: Host provided no provider-run identity"
            | Some profile, Some _, None -> return Error "return rejected: SyncDelegate has no active caller"
            | Some profile, Some toolRun, Some call when call.Delegate <> delegateSession ->
                return Error "return rejected: delegate does not own the active sync call"
            | Some profile, Some toolRun, Some call when canonicalRole call.Role <> profile.CanonicalRole ->
                return Error "return rejected: active profile role does not match SyncDelegate binding"
            | Some _, Some toolRun, Some call ->
                if tryPendingText delegateSession |> Option.isSome then
                    return Error "return rejected: SyncDelegate return completion is already pending"
                else
                    armPendingText
                        delegateSession
                        { Text = SyncDelegatePrompt.SyncDelegateReturnCompletion
                          ToolRun = toolRun }

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

            match tryPendingText sessionId with
            | Some pending when completionRun <> pending.ToolRun -> output?text <- pending.Text
            | _ -> ()

    member _.HandleTurn(turn: ReconciledTurn, permit: QuiescencePermit option) : Task<bool> =
        task {
            match turn.Role, tryCallByDelegate turn.SessionId with
            | Some(Role.Inspector | Role.Coder), Some call ->
                match turn.Outcome with
                | ReconcileProgram.TurnCompleted ->
                    let payload = normalizePayload (CompletedTurnClassifier.partsText turn.Parts)

                    if payload = normalizePayload SyncDelegatePrompt.SyncDelegateReturnCompletion then
                        clearPendingText turn.SessionId
                        removeCall call
                        AsyncSupport.trySetResult call.Completion (Ok()) |> ignore
                        return true
                    else
                        match call.Nudges >= recoveryBudget with
                        | true ->
                            clearPendingText turn.SessionId
                            removeCall call

                            failCall
                                call
                                (sprintf "SyncDelegate idle recovery budget exhausted after %i nudges" recoveryBudget)

                            return true
                        | false ->
                            match! sendIdleNudge permit call with
                            | Ok _ ->
                                updateCall call.OwnerScope (fun current ->
                                    { current with
                                        Nudges = current.Nudges + 1 })
                                |> ignore
                            | Error _ -> ()

                            return true
                | ReconcileProgram.TurnFailed error
                | ReconcileProgram.TurnAborted error ->
                    clearPendingText turn.SessionId
                    removeCall call
                    failCall call (sprintf "SyncDelegate run failed: %s" error)
                    return true
                | _ -> return true
            | _ -> return false
        }

    member _.CancelSession(sessionId: SessionId) : unit =
        let asOwnerScope = ReuseScope.ofSession sessionId

        let call =
            tryCallByOwnerScope asOwnerScope
            |> Option.orElseWith (fun () -> tryCallByDelegate sessionId)

        // Capture inspector draft holders before bindings are torn down.
        let inspectorOwned = attached.TryFind(sessionId, SyncDelegateRole.Inspector)

        let inspectorAsDelegate =
            match call with
            | Some c when c.Role = SyncDelegateRole.Inspector -> Some c.Delegate
            | _ -> None

        call
        |> Option.iter (fun scope ->
            clearPendingText scope.Delegate
            removeCall scope
            failCall scope "Sync delegate call was cancelled"
            releaseFlight scope.OwnerScope)

        clearPendingText sessionId
        attached.RemoveByDelegateSession sessionId |> ignore

        for role in [ SyncDelegateRole.Inspector; SyncDelegateRole.Coder ] do
            attached.Remove(sessionId, role) |> ignore

        inspectorOwned |> Option.iter (fun id -> cleanupInspectorDraft (sessionKey id))

        inspectorAsDelegate
        |> Option.iter (fun id -> cleanupInspectorDraft (sessionKey id))

        // Deleted id may itself hold an inspector draft (no owner binding left).
        cleanupInspectorDraft (sessionKey sessionId)

    member _.Dispose() =
        lock gate (fun () ->
            for call in callsByOwnerScope.Values |> Seq.toList do
                failCall call "SyncDelegate runtime disposed"

            callsByOwnerScope.Clear()
            callsByDelegate.Clear()
            pendingCompletionTexts.Clear()
            inFlightScopes.Clear()
            attached.Clear())

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
