namespace Wanxiangshu.Execution.Delegation.SyncDelegate

open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Host
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode

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
        workRecordFor: SessionId -> MagicTodoLwr.BoundedRange -> ProviderRunIdentity -> Task<string option>,
        handoff: ReusableHandoffPort,
        ?workspaceDirectory: string,
        /// Casebook draft hooks (wired from SpikePlugin → CasebookLifecycle; compile-order seam).
        ?onInspectorPrompt: string -> string -> unit,
        ?onInspectorAnswer: string -> string -> unit,
        ?onInspectorCleanup: string -> unit
    ) =
    let store = SyncDelegateCallStore()
    let directory = workspaceDirectory
    let noteInspectorPrompt = defaultArg onInspectorPrompt (fun _ _ -> ())
    let noteInspectorAnswer = defaultArg onInspectorAnswer (fun _ _ -> ())
    let cleanupInspectorDraft = defaultArg onInspectorCleanup (fun _ -> ())
    let projectWorkRecord = workRecordFor

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
        taskResult {
            let tools = toolMap (canonicalRole call.Role)
            let route = DelegationHandoffRoute.syncRole call.OwnerScope call.Role
            let! prepared = handoff.Prepare call.Owner route |> TaskResultCE.ofTask

            // EXEC-031: capture the Opening from the raw Charge (not the
            // provider envelope), matching OneShotAgentTool. PromptIngress omits
            // Opening for AgentOwnerRoot, so the LWR projector would otherwise
            // return None and the bounded record would be undefined. Idempotent:
            // a reused child keeps its first invocation's Opening (PERSIST-010).
            do!
                XTraceCapture.captureOpening (Some journal) call.Delegate request.Charge []
                |> TaskResultCE.ofTask

            // EXEC-031: snapshot the child's XTrace head (one-past last part,
            // 0 when empty) at send. This is the inclusive start of the
            // per-invocation range; the exclusive end is the same head
            // captured at completion. All coalesced invocations in this
            // call share the same head and thus the same bounded record.
            let startCursor = xTraceHead call.Delegate

            for inv in call.Invocations do
                inv.StartCursor <- Some startCursor

            let providerPrompt =
                DelegationHandoff.appendParentDelta request.ProviderPrompt prepared.ParentRecord

            let! _ =
                dispatcher.SendAgentOwnerRootWithTools
                    sessions
                    call.Delegate
                    providerPrompt
                    call.Agent
                    directory
                    PromptDispatcher.AwaitMode.Await
                    (Some(fun physical ->
                        let root = PhysicalUserMessageId.promoteToAuthorityRoot physical
                        call.AcceptedAuthorityRoot <- Some root
                        AsyncSupport.trySetResult call.AcceptedRoot root |> ignore))
                    tools
                    None

            let! _ = call.AcceptedRoot.Task |> TaskResultCE.ofTask
            return prepared
        }

    let deps: SyncDelegateWorkflow.Dependencies =
        { Attached = attached
          ResolveOwnerTier = resolveOwnerTier
          CreateChild = createChild
          OnDelegateReady = onDelegateReady
          NoteInspectorPrompt = noteInspectorPrompt
          CleanupInspectorDraft = cleanupInspectorDraft
          Directory = directory
          ReplaceToolEstimate =
            fun sessionId expectedToolCalls ->
                task {
                    match expectedToolCalls with
                    | Some expected -> do! DelegatedToolEstimateLedger.replace journal sessionId expected
                    | None -> ()
                }
          SendPrompt = fun call request -> sendDelegatePrompt call request
          CheckpointCompletedHandoff = fun parent prepared -> handoff.CheckpointCompleted parent prepared
          ResolveBoundAgent =
            fun childId ->
                let projections = (AgentJournal.snapshot journal).AgentProjections

                PromptAuthorityLedger.activeProfile childId projections
                |> Option.orElseWith (fun () -> PromptAuthorityLedger.lastAuthorityProfile childId projections)
                |> Option.map (fun profile -> profile.SelectedAgent)
                |> Option.filter (String.IsNullOrWhiteSpace >> not)
          DescribeWait = SyncDelegateWait.describe
          SubscribeFutureTerminal = fun sessionId listener -> sessions.SubscribeFutureTerminal(sessionId, listener) }

    let failPoppedCalls delegateSessionId reason =
        let rec popAll () =
            match store.TryPopCallByDelegate delegateSessionId with
            | Some call ->
                store.FailCall(call, reason)
                popAll ()
            | None -> ()

        popAll ()

    let resolveWorkRecord
        (turnSessionId: SessionId)
        (call: SyncDelegateCall)
        (endCursor: int64)
        (providerRun: ProviderRunIdentity)
        =
        match call.Invocations |> List.tryHead |> Option.bind (fun inv -> inv.StartCursor) with
        | None -> Task.FromResult None
        | Some startCursor ->
            projectWorkRecord
                turnSessionId
                { StartInclusive = { Sequence = startCursor }
                  EndExclusive = { Sequence = endCursor } }
                providerRun

    let noteInspectorIfRole (call: SyncDelegateCall) turnSessionId record =
        if call.Role = SyncDelegateRole.Inspector then
            noteInspectorAnswer (sessionKey turnSessionId) record

    let finishCompletedCall turnSessionId (call: SyncDelegateCall) workRecord =
        match workRecord with
        | Some record when not (String.IsNullOrWhiteSpace record) ->
            noteInspectorIfRole call turnSessionId record
            AsyncSupport.trySetResult call.Answer (Ok record) |> ignore
            true
        | _ ->
            // EXEC-031 / EXEC-026: fail closed. A Completed turn
            // without a bounded WorkRecord is a protocol defect, not
            // a licence to fall back to the last message (EXEC-028
            // residual OneShot analog). The session stays reusable.
            store.FailCall(call, "EXEC-031: Completed without bounded WorkRecord")
            true

    let handleCompletedCall (turn: ReconciledTurn) (call: SyncDelegateCall) =
        task {
            // Completion marker for ManagerLife/Reviewer. HandleTurn
            // does not use Terminal to build the inspect payload;
            // the bounded WorkRecord is the invocation's parts range.
            do! XTraceCapture.captureTerminal (Some journal) turn

            // Exclusive range end = XTrace.head (one-past last part).
            let endCursor = xTraceHead turn.SessionId

            let! workRecord = resolveWorkRecord turn.SessionId call endCursor turn.ProviderRun
            return finishCompletedCall turn.SessionId call workRecord
        }

    let popIfAuthorityMatches
        (store: SyncDelegateCallStore)
        (turn: ReconciledTurn)
        (call: SyncDelegateCall)
        : Task<SyncDelegateCall option> =
        task {
            let! expectedRoot = call.AcceptedRoot.Task

            return
                if expectedRoot = turn.AuthorityRootUserMessageId then
                    store.TryPopCallByDelegate turn.SessionId
                else
                    None
        }

    let tryConsumeReadyCall (store: SyncDelegateCallStore) (turn: ReconciledTurn) : Task<SyncDelegateCall option> =
        let candidate =
            store.TryPeekCallByDelegate turn.SessionId
            |> Option.filter (fun call ->
                call.Invocations
                |> List.forall (fun invocation -> invocation.StartCursor.IsSome))

        match candidate with
        | Some call -> popIfAuthorityMatches store turn call
        | None -> Task.FromResult None

    let handleCompletedRoleTurn (turn: ReconciledTurn) =
        task {
            match! tryConsumeReadyCall store turn with
            | Some call -> return! handleCompletedCall turn call
            | None -> return false
        }

    let singletonResult taskResult =
        task {
            match! taskResult with
            | Ok(SyncDelegateInvocationResult.WorkRecord workRecord) -> return Ok workRecord
            | Ok(SyncDelegateInvocationResult.MergedInto _) ->
                return Error "sync delegate protocol defect: singleton invocation was merged"
            | Error error -> return Error error
        }

    member _.Attached = attached

    member _.ObserveProviderToolCall
        (ownerSessionId: SessionId, providerRun: ProviderRunIdentity, role: SyncDelegateRole, callId: ToolCallId)
        =
        store.ObserveProviderToolCall(ownerSessionId, providerRun, role, callId)

    member _.TryObservedBatch
        (ownerSessionId: SessionId, providerRun: ProviderRunIdentity, role: SyncDelegateRole, currentCall: ToolCallId) =
        store.TryObservedBatch(ownerSessionId, providerRun, role, currentCall)

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
            failPoppedCalls inspectorSessionId "Sync delegate Inspector session was deleted"

            attached.Remove(ownerSessionId, SyncDelegateRole.Inspector) |> ignore

            let ownerScope = ReuseScope.ofSession ownerSessionId

            let replaced = store.PutDeletedInspector(ownerScope, inspectorSessionId)

            replaced
            |> Option.filter (fun previous -> previous <> inspectorSessionId)
            |> Option.iter (fun previous -> cleanupInspectorDraft (sessionKey previous))

            true
        | _ -> false

    member _.Invoke
        (ownerSessionKey: string, role: SyncDelegateRole, charge: string, ?expectedToolCalls: int)
        : Task<Result<string, string>> =
        SyncDelegateWorkflow.invoke store deps ownerSessionKey role charge expectedToolCalls None (fun () ->
            Task.FromResult charge)
        |> singletonResult

    /// EXEC-032 composition seam: caller supplies a low-trust provider prompt
    /// producer; workflow invokes it only after semantic batch admission.
    member _.InvokePrepared
        (
            ownerSessionKey: string,
            role: SyncDelegateRole,
            charge: string,
            prepareProviderPrompt: unit -> Task<string>,
            ?expectedToolCalls: int
        ) : Task<Result<string, string>> =
        SyncDelegateWorkflow.invoke store deps ownerSessionKey role charge expectedToolCalls None prepareProviderPrompt
        |> singletonResult

    member _.InvokeBatchPrepared
        (
            ownerSessionKey: string,
            role: SyncDelegateRole,
            charge: string,
            batch: SyncDelegateBatch,
            prepareProviderPrompt: unit -> Task<string>,
            ?expectedToolCalls: int
        ) : Task<Result<SyncDelegateInvocationResult, string>> =
        SyncDelegateWorkflow.invoke
            store
            deps
            ownerSessionKey
            role
            charge
            expectedToolCalls
            (Some batch)
            prepareProviderPrompt

    member _.HandleTurn(turn: ReconciledTurn, permit: QuiescencePermit option) : Task<bool> =
        task {
            match turn.Role, turn.Outcome with
            | Some(Role.Inspector | Role.Coder), ReconcileProgram.TurnCompleted -> return! handleCompletedRoleTurn turn
            | _ ->
                // Transient failures (TurnFailed / TurnInProgress / TurnNeedsContinuation)
                // remain child-local and allow ordinary fallback recovery to proceed.
                // Only exhausted retries or definitive aborts (via SubscribeTerminal)
                // report failure to the caller.
                return false
        }

    /// True once the provider-owned opening capture has assigned a bounded cursor.
    member _.HasOpeningCursor(sessionId: SessionId) : bool =
        match store.TryPeekCallByDelegate sessionId with
        | Some call ->
            call.Invocations.Length > 0
            && call.Invocations
               |> List.forall (fun invocation -> invocation.StartCursor.IsSome)
        | None -> false

    member _.AwaitAssignmentReady(sessionId: SessionId) : Task<bool> =
        match store.TryPeekCallByDelegate sessionId with
        | None -> Task.FromResult false
        | Some call ->
            task {
                let! _ = call.AcceptedRoot.Task
                return true
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
