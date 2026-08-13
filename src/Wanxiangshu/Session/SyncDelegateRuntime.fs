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
        /// SendPrompt. Test override for a single model; production uses
        /// `promptModelFor` so mixed Deep/Fast owners keep their own binding.
        ?promptModel: OpencodeModel,
        /// EXEC-031: per-invocation bounded WorkRecord projector
        /// (includeOpening=false). Injected from plugin wiring so Session does
        /// not depend on Finality compile order; range = the invocation's
        /// XTrace [StartInclusive, EndExclusive).
        ?workRecordFor: SessionId -> MagicTodoLwr.BoundedRange -> Task<string option>,
        /// Per-send model lookup from the Host-final agent inventory.
        /// `promptModel` remains a test override; production must not pass a
        /// process-wide model, or mixed Deep/Fast owners would collapse.
        ?promptModelFor: (string -> OpencodeModel option)
    ) =
    let store = SyncDelegateCallStore()
    let directory = workspaceDirectory
    let noteInspectorPrompt = defaultArg onInspectorPrompt (fun _ _ -> ())
    let noteInspectorAnswer = defaultArg onInspectorAnswer (fun _ _ -> ())
    let cleanupInspectorDraft = defaultArg onInspectorCleanup (fun _ -> ())
    let boundPromptModel = promptModel
    let projectWorkRecord = defaultArg workRecordFor (fun _ _ -> Task.FromResult None)
    let modelFor (agent: string) =
        match boundPromptModel with
        | Some model -> Some model
        | None -> promptModelFor |> Option.bind (fun lookup -> lookup agent)

    let sessionKey (sessionId: SessionId) = SessionId.value sessionId

    let scopeKey (scope: ReuseScopeId) = ReuseScopeId.value scope

    let canonicalRole =
        function
        | SyncDelegateRole.Inspector -> Role.Inspector
        | SyncDelegateRole.Coder -> Role.Coder

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

    let xTraceHead (sessionId: SessionId) : int64 =
        AgentJournal.snapshot journal
        |> fun snapshot -> AgentProjection.tryFind sessionId snapshot.AgentProjections
        |> Option.bind (fun session -> session.XTrace)
        |> Option.map XTraceProjection.head
        |> Option.defaultValue 0L

    let sendDelegatePrompt (call: SyncDelegateCall) (request: SyncDelegatePromptRequest) =
        task {
            let tools = toolMap (canonicalRole call.Role)

            // EXEC-031: capture the Opening from the raw Charge (not the
            // provider envelope), matching OneShotAgentTool. PromptIngress omits
            // Opening for AgentOwnerRoot, so the LWR projector would otherwise
            // return None and the bounded record would be undefined. Idempotent:
            // a reused child keeps its first invocation's Opening (PERSIST-010).
            do! XTraceCapture.captureOpening (Some journal) call.Delegate request.Charge []

            // EXEC-031: snapshot the child's XTrace head (one-past last part,
            // 0 when empty) at send. This is the inclusive start of the
            // per-invocation range; the exclusive end is the same head
            // captured at completion. All coalesced invocations in this
            // call share the same head and thus the same bounded record.
            let startCursor = xTraceHead call.Delegate

            for inv in call.Invocations do
                inv.StartCursor <- Some startCursor

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
                    (modelFor call.Agent)
        }

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
          ResolveBoundAgent =
            fun childId ->
                let projections = (AgentJournal.snapshot journal).AgentProjections

                PromptAuthorityLedger.activeProfile childId projections
                |> Option.orElseWith (fun () -> PromptAuthorityLedger.lastAuthorityProfile childId projections)
                |> Option.map (fun profile -> profile.SelectedAgent)
                |> Option.filter (String.IsNullOrWhiteSpace >> not)
          DescribeWait = SyncDelegateWait.describe }

    let singletonResult taskResult =
        task {
            match! taskResult with
            | Ok(SyncDelegateInvocationResult.WorkRecord workRecord) -> return Ok workRecord
            | Ok(SyncDelegateInvocationResult.MergedInto _) ->
                return Error "sync delegate protocol defect: singleton invocation was merged"
            | Error error -> return Error error
        }

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
        SyncDelegateWorkflow.invoke store deps ownerSessionKey role charge None (fun () -> Task.FromResult charge)
        |> singletonResult

    /// EXEC-032 composition seam: caller supplies a low-trust provider prompt
    /// producer; workflow invokes it only after semantic batch admission.
    member _.InvokePrepared
        (ownerSessionKey: string, role: SyncDelegateRole, charge: string, prepareProviderPrompt: unit -> Task<string>) : Task<
                                                                                                                             Result<
                                                                                                                                 string,
                                                                                                                                 string
                                                                                                                              >
                                                                                                                          >
        =
        SyncDelegateWorkflow.invoke store deps ownerSessionKey role charge None prepareProviderPrompt
        |> singletonResult

    member _.InvokeBatchPrepared
        (
            ownerSessionKey: string,
            role: SyncDelegateRole,
            charge: string,
            batch: SyncDelegateBatch,
            prepareProviderPrompt: unit -> Task<string>
        ) : Task<Result<SyncDelegateInvocationResult, string>> =
        SyncDelegateWorkflow.invoke store deps ownerSessionKey role charge (Some batch) prepareProviderPrompt

    member _.HandleTurn(turn: ReconciledTurn, permit: QuiescencePermit option) : Task<bool> =
        task {
            match turn.Role with
            | Some(Role.Inspector | Role.Coder) ->
                match store.TryPopCallByDelegate turn.SessionId with
                | Some call ->
                    match turn.Outcome with
                    | ReconcileProgram.TurnCompleted ->
                        // Completion marker for ManagerLife/Reviewer. HandleTurn
                        // does not use Terminal to build the inspect payload;
                        // the bounded WorkRecord is the invocation's parts range.
                        do! XTraceCapture.captureTerminal (Some journal) turn

                        // Exclusive range end = XTrace.head (one-past last part).
                        let endCursor = xTraceHead turn.SessionId

                        let! workRecord =
                            match call.Invocations |> List.tryHead |> Option.bind (fun inv -> inv.StartCursor) with
                            | None -> Task.FromResult None
                            | Some startCursor ->
                                projectWorkRecord
                                    turn.SessionId
                                    { StartInclusive = { Sequence = startCursor }
                                      EndExclusive = { Sequence = endCursor } }

                        match workRecord with
                        | Some record when not (String.IsNullOrWhiteSpace record) ->
                            if call.Role = SyncDelegateRole.Inspector then
                                noteInspectorAnswer (sessionKey turn.SessionId) record

                            AsyncSupport.trySetResult call.Answer (Ok record) |> ignore
                            return true
                        | _ ->
                            // EXEC-031 / EXEC-026: fail closed. A Completed turn
                            // without a bounded WorkRecord is a protocol defect, not
                            // a licence to fall back to the last message (EXEC-028
                            // residual OneShot analog). The session stays reusable.
                            store.FailCall(call, "EXEC-031: Completed without bounded WorkRecord")
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
