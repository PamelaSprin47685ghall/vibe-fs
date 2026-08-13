namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Finality
open Wanxiangshu.Host
open Wanxiangshu.Infrastructure
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

module PluginSessionWiring =

    /// SyncDelegate + StrengthReplica runtimes, attached only when a durable
    /// journal exists (the sync path is what makes both runtimes meaningful).
    let attach (boot: PluginBoot.Boot) (host: PluginHostWiring.Host) : unit =
        let scope = boot.Scope
        let journal = boot.Journal
        let sessionPort = host.SessionPort
        let wired = host.Wired
        let workspaceDirectory = boot.WorkspaceDirectory

        match journal with
        | Some durable ->
            // SyncDelegate attaches whenever the durable journal exists.
            let attached =
                AttachedSessionRuntime(
                    registerParent =
                        (fun owner child ->
                            scope.Sessions.SessionParents.[SessionId.value child] <- SessionId.value owner),
                    isUsable = (fun _ -> true)
                )

            let dispatcher = PromptDispatcher.forJournal durable
            let promptModelFor agent =
                scope.Strength.ManagedAgentInventory
                |> Option.bind (fun inventory -> ManagedAgentConfig.tryOpencodeModel inventory agent None)


            let registerDelegate (delegateId: SessionId) (agent: string) =
                wired.RegisterOwned(SessionId.value delegateId)

                let role =
                    if agent.Contains "coder" then
                        Role.Coder
                    else
                        Role.Inspector

                wired.BindActiveRun delegateId role workspaceDirectory

            let syncDelegate =
                new SyncDelegateRuntime(
                    sessionPort,
                    dispatcher,
                    durable,
                    attached,
                    SyncDelegateTier.fromDispatcher dispatcher,
                    registerDelegate,
                    scope.Sessions.Quiescence,
                    ?workspaceDirectory = workspaceDirectory,
                    ?onInspectorPrompt = Some CasebookLifecycle.notePrompt,
                    ?onInspectorAnswer = Some CasebookLifecycle.noteAnswer,
                    ?onInspectorCleanup = Some CasebookLifecycle.cleanupInspector,
                    ?workRecordFor =
                        Some(fun sessionId range ->
                            LifecycleWorkRecordProjection.lifecycleWorkRecordBounded (Some durable) sessionId range),
                    ?promptModelFor = Some promptModelFor
                )

            scope.AttachSyncDelegateRuntime syncDelegate

            let registerStrengthReplica (ownerId: SessionId) (replicaId: SessionId) (agent: string) =
                let ownerKey = SessionId.value ownerId
                let replicaKey = SessionId.value replicaId
                scope.Sessions.SessionParents.[replicaKey] <- ownerKey
                wired.RegisterOwned ownerKey
                wired.RegisterOwned replicaKey

                match ManagedAgent.tryParse agent with
                | Some managed -> wired.BindActiveRun replicaId managed.Role workspaceDirectory
                | None -> ()

            let strengthReplicaRuntime =
                new StrengthReplicaRuntime(
                    sessionPort,
                    dispatcher,
                    Wanxiangshu.Process.PtyTiming.nodeTimerPort (),
                    scope.Strength.StrengthRuntime,
                    registerStrengthReplica,
                    ?workspaceDirectory = workspaceDirectory,
                    ?promptModelFor = Some promptModelFor
                )

            scope.Strength.AttachStrengthReplicaRuntime strengthReplicaRuntime
        | None -> ()
