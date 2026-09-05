namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
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
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
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
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Persistence.Journal
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
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

module PluginHostWiring =

    /// Composition-root handle for everything the Host needs after boot:
    /// the ports `HostSignalBootstrap.wire` produced plus the durability
    /// handle and the shared-terminal acquisition from `PluginHost.createHost`.
    type Host =
        { EventPort: IEventObservationPort
          SessionPort: ISessionHostPort
          SnapshotOpt: ISessionSnapshotPort option
          Wired: HostSignalBootstrap.WiredSignals
          SharedTerminalKey: string option
          SharedTerminalPort: Events.HostEventPort option
          StrengthDurability: StrengthDurabilityPort option
          RootWorkspace: IRootWorkspaceReader
          CausalWaitObserver: IWaitObserver }

    let create (boot: PluginBoot.Boot) : Task<Host> =
        task {
            let input = boot.Input
            let scope = boot.Scope
            let workspaceDirectory = boot.WorkspaceDirectory

            let completeHost eventPort sessionPort snapshotOpt terminalKey sharedTerminalPort : Task<Host> =
                task {
                    match boot.Journal with
                    | Some journal ->
                        BookkeeperRuntime.setRuntime sessionPort (fun ownerSessionId ->
                            let projections = (AgentJournal.snapshot journal).AgentProjections
                            PromptAuthorityLedger.activeProfile ownerSessionId projections)
                    | None -> BookkeeperRuntime.resetRuntime ()

                    scope.AttachSharedTerminal(terminalKey, sharedTerminalPort)
                    scope.AttachSatelliteRuntime(SatelliteRuntime sessionPort)

                    for KeyValue(childId, parentId) in scope.Sessions.SessionParents do
                        scope.Sessions.OwnedSessions.Add childId |> ignore
                        scope.Sessions.OwnedSessions.Add parentId |> ignore

                    // STRENGTH-006..008: borrow the same unified EventStore already
                    // acquired by AgentJournal boot.
                    let strengthDurability =
                        match boot.Journal, workspaceDirectory with
                        | Some _, Some workspace ->
                            WorkspaceEventStore.tryCurrent (RuntimePath.gitCommonDir workspace)
                            |> Option.map StrengthDurability.create
                        | _ -> None

                    let causalWait = CausalWaitProcess.local ()
                    let rootWorkspace = RootWorkspaceProcess.local ()

                    // Keep the causal wait bridge on the root workspace.
                    rootWorkspace.Binder.TryBind workspaceDirectory |> ignore

                    rootWorkspace.Reader.TryRead()
                    |> Option.iter (fun workspace ->
                        causalWait.BindDiagnosticTarget(CausalWaitBridge.target workspace) |> ignore)

                    CasebookLifecycle.setEnabled workspaceDirectory

                    let! wired =
                        HostSignalBootstrap.wire
                            sessionPort
                            eventPort
                            snapshotOpt
                            boot.Journal
                            strengthDurability
                            scope
                            rootWorkspace.Reader
                            input
                            BookkeeperRuntime.tryConsumePromptAuthorization
                            (fun terminal ->
                                let outcome =
                                    match terminal.Outcome with
                                    | HostProviderTerminalOutcome.Completed _ -> Ok()
                                    | failure -> Error(sprintf "%A" failure)

                                BookkeeperRuntime.completePhysical terminal.SessionId outcome)
                            workspaceDirectory
                            (Some CasebookLifecycle.tryFinalizeInspector)
                            (Some CasebookLifecycle.cleanupInspector)

                    return
                        { EventPort = eventPort
                          SessionPort = sessionPort
                          SnapshotOpt = snapshotOpt
                          Wired = wired
                          SharedTerminalKey = terminalKey
                          SharedTerminalPort = sharedTerminalPort
                          StrengthDurability = strengthDurability
                          RootWorkspace = rootWorkspace.Reader
                          CausalWaitObserver = causalWait.Observer }
                }

            let isLifecycleTerminated (sessionId: SessionId) =
                match boot.Journal with
                | None -> false
                | Some durable ->
                    let snapshot = AgentJournal.snapshot durable
                    AgentProjection.tryFind sessionId snapshot.AgentProjections
                    |> Option.bind (fun (s: SessionAgentProjection) -> s.Relay)
                    |> Option.bind (fun (r: RelayState) -> Fold.view r (RoadId.create (SessionId.value sessionId)))
                    |> Option.bind (fun road -> road.LatestRetirement)
                    |> Option.isSome

            match PluginHost.createHost input boot.PortOpt (Some boot.FamilyParent) (Some isLifecycleTerminated) with
            | Error err -> return raise (InvalidOperationException err)
            | Ok(eventPort, sessionPort, snapshotOpt, terminalKey, sharedTerminalPort) ->
                return! completeHost eventPort sessionPort snapshotOpt terminalKey sharedTerminalPort
        }
