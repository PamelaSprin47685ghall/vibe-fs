namespace Wanxiangshu.OpenCode

open Wanxiangshu.Execution.Delegation

#nowarn "3511"

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
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
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Host
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Knowledge.Casebook.OpenCode
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence
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
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Strength

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

            let syncDelegate =
                new SyncDelegateRuntime(
                    sessionPort,
                    dispatcher,
                    durable,
                    attached,
                    SyncDelegateTier.fromDispatcher dispatcher,
                    registerDelegate,
                    scope.Sessions.Quiescence,
                    (fun sessionId range providerRun ->
                        LifecycleWorkRecordProjection.lifecycleWorkRecordBoundedForRun
                            (Some durable)
                            sessionId
                            range
                            providerRun),
                    DelegationHandoffLedger.port durable,
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

            let strengthReplicaRuntime =
                new StrengthReplicaRuntime(
                    sessionPort,
                    dispatcher,
                    scope.Strength.StrengthRuntime,
                    registerStrengthReplica,
                    ?workspaceDirectory = workspaceDirectory,
                    ?tryAcquireModel =
                        Some(fun sessionId agent ->
                            ModelRouting.tryReserveManaged sessionId agent
                            |> Option.map ModelRouting.toOpenCodeModel),
                    ?releaseModel = Some(fun sessionId -> ModelRouting.releaseExecution sessionId |> ignore)
                )

            scope.Strength.AttachStrengthReplicaRuntime strengthReplicaRuntime
        | None -> ()
