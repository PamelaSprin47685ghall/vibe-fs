namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Strength.Persistence

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

    /// Callback supplier: wiring hands the bound ports, the composition root
    /// returns the turn-workflow observation task. Keeps Host-side modules
    /// free of any static reference to the relay turn-workflow module.
    type ObserveTurnWorkflowSupplier =
        ISessionHostPort -> IEventObservationPort -> IRootWorkspaceReader -> AbortCause -> ReconciledTurnContext -> Task

    let create (observeTurnWorkflowFor: ObserveTurnWorkflowSupplier) (boot: PluginBoot.Boot) : Task<Host> =
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
                            PromptAuthorityProjectionQueries.activeProfile ownerSessionId projections)
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

                    // Strength closures are built here in plugin composition from
                    // the already-held scope and durability handle; the Host
                    // boundary only receives the neutral `StrengthHostPorts`.
                    let strengthPorts =
                        PluginStrengthPorts.create (Some boot.StrengthScope) strengthDurability

                    let! wired =
                        HostSignalBootstrap.wire
                            (observeTurnWorkflowFor sessionPort eventPort rootWorkspace.Reader)
                            sessionPort
                            eventPort
                            snapshotOpt
                            boot.Journal
                            strengthPorts
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
                            (let tryFinalize workspaceRoot inspectorSessionId =
                                try
                                    let commonDir = RuntimePath.gitCommonDir workspaceRoot
                                    let store = WorkspaceEventStore.acquire commonDir
                                    CasebookLifecycle.tryFinalizeInspector workspaceRoot store inspectorSessionId
                                with ex ->
                                    Task.FromResult(Error ex.Message)
                            
                            Some tryFinalize)
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
