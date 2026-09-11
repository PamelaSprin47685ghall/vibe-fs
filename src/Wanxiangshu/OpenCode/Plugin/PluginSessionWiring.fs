namespace Wanxiangshu.OpenCode

open Wanxiangshu.Execution.Delegation

#nowarn "3511"

open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Strength.Replica

module PluginSessionWiring =

    /// SyncDelegate + StrengthReplica runtimes, attached only when a durable
    /// journal exists (the sync path is what makes both runtimes meaningful).
    let attach (boot: PluginBoot.Boot) (host: PluginHostWiring.Host) : unit =
        let scope = boot.Scope
        let journal = boot.Journal
        let sessionPort = host.SessionPort
        let wired = host.Wired
        let workspaceDirectory = boot.WorkspaceDirectory

        let roleForAgent (agent: string) : Role =
            if agent.Contains "coder" then
                Role.Coder
            else
                Role.Inspector

        let bindStrengthReplica replicaId agent =
            match ManagedAgent.tryParse agent with
            | Some managed -> wired.BindActiveRun replicaId managed.Role workspaceDirectory
            | None -> ()

        let seedDurableSessions (durable: AgentJournal) =
            let snapshot = AgentJournal.snapshot durable

            snapshot.AgentProjections.Sessions
            |> Map.iter (fun sessionId sessionProj ->
                sessionProj.PromptAuthority
                |> Option.bind (fun authority -> authority.ActiveLogicalRun)
                |> Option.iter (fun profile ->
                    let agent = profile.SelectedAgent
                    SessionExecutionBinding.observeUserFacingAgent sessionId agent
                    scope.Sessions.ModelRoutingSessions.Add(SessionId.value sessionId) |> ignore
                    ProviderLanguageBinding.ensureRoot sessionId |> ignore))

        match journal with
        | Some durable ->
            // DURABLE-EVENTS-020: history-derived process bindings are semantic
            // state, so seeding them belongs to the first durable admission, not
            // plugin construction. This callback also forces the deferred
            // WorkspaceEventStore Current exactly at that activation boundary.
            scope.AttachDurabilityActivation(fun () -> seedDurableSessions durable)

            // SyncDelegate attaches whenever the durable journal exists.
            let attached =
                AttachedSessionRuntime(
                    registerParent =
                        (fun owner child ->
                            scope.Sessions.SessionParents.[SessionId.value child] <- SessionId.value owner),
                    isUsable = (fun _ -> true)
                )

            let dispatcher = PromptDispatcher.forJournal durable

            let registerDelegate (delegateId: SessionId) (agent: string) =
                wired.RegisterOwned(SessionId.value delegateId)

                wired.BindActiveRun delegateId (roleForAgent agent) workspaceDirectory

            let workRecordCapability: DelegationWorkRecordCapability =
                { ParentWorkRecord =
                    fun sessionId -> LifecycleWorkRecordProjection.lifecycleWorkRecord (Some durable) sessionId true
                  ParentWorkRecordBounded =
                    fun sessionId range ->
                        LifecycleWorkRecordProjection.lifecycleWorkRecordBounded (Some durable) sessionId range }

            let syncDelegate =
                new SyncDelegateRuntime(
                    sessionPort,
                    CausalAwait.awaitTask host.CausalWaitObserver,
                    CausalAwait.awaitTask host.CausalWaitObserver,
                    dispatcher,
                    durable,
                    (attached :> IAttachedSessionPort),
                    registerDelegate,
                    scope.Sessions.Quiescence,
                    (fun sessionId range providerRun ->
                        LifecycleWorkRecordProjection.lifecycleWorkRecordBoundedForRun
                            (Some durable)
                            sessionId
                            range
                            providerRun),
                    DelegationHandoffLedger.port workRecordCapability durable,
                    toolMapForRole =
                        (fun role ->
                            PromptAuthority.toolCapabilitiesFor role ProviderRequestKind.WorkMain
                            |> StaticTools.requestToolMap),
                    ?workspaceDirectory = workspaceDirectory,
                    ?onInspectorPrompt = Some CasebookLifecycle.notePrompt,
                    ?onInspectorAnswer = Some CasebookLifecycle.noteAnswer,
                    ?onInspectorCleanup = Some CasebookLifecycle.cleanupInspector
                )

            scope.AttachSyncDelegateRuntime syncDelegate

            let registerStrengthReplica (ownerId: SessionId) (replicaId: SessionId) (agent: string) =
                let ownerKey = SessionId.value ownerId
                let replicaKey = SessionId.value replicaId
                scope.Sessions.SessionParents.[replicaKey] <- ownerKey
                wired.RegisterOwned ownerKey
                wired.RegisterOwned replicaKey

                bindStrengthReplica replicaId agent

            let tryParentKey sessionId =
                let found, parentKey =
                    scope.Sessions.SessionParents.TryGetValue(SessionId.value sessionId)

                if found then Some parentKey else None

            let strengthReplicaRuntime =
                new StrengthReplicaRuntime(
                    sessionPort,
                    dispatcher,
                    boot.StrengthScope.StrengthRuntime,
                    registerStrengthReplica,
                    ?workspaceDirectory = workspaceDirectory,
                    ?tryAcquireModel =
                        Some(fun sessionId agent ->
                            let role = roleForAgent agent

                            let lenderSessionId = tryParentKey sessionId

                            ModelRouting.tryReserveManaged sessionId role lenderSessionId
                            |> Option.map ModelRouting.toOpenCodeModel),
                    ?releaseModel = Some(fun sessionId -> ModelRouting.releaseExecution sessionId |> ignore)
                )

            boot.StrengthScope.AttachStrengthReplicaRuntime strengthReplicaRuntime
        | None -> ()
