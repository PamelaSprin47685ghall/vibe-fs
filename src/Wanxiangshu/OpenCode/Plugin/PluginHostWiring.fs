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

    /// Graceful close: finalize the root-owned inspector draft through the
    /// workspace store once. Acquiring the store is pre-validated by the
    /// outer `try`; once held, lifecycle failures report via the task's
    /// settled signature instead of retrying the pyramid.
    let private tryFinalizeWithin workspaceRoot store delegateSessionId =
        task {
            try
                return! CasebookLifecycle.tryFinalizeDraft workspaceRoot store delegateSessionId

            with ex ->
                // A thrown boundary error (store acquisition or lifecycle
                // bug) has indeterminate durability: report Unknown and
                // retain the identity.
                return CaseFinalizeSettlement.unknown delegateSessionId ex.Message
        }

    /// Workspace root–DelegateSessionId → settlement. The two layers of
    /// indeterminate-durability failure compose at the module boundary: store
    /// acquisition collapses to Unknown, lifecycle failure does too, and the
    /// detached Task is handed to the caller unchanged.
    let private tryFinalize workspaceRoot delegateSessionId =
        try
            let commonDir = RuntimePath.gitCommonDir workspaceRoot
            let store = WorkspaceEventStore.acquire commonDir
            tryFinalizeWithin workspaceRoot store delegateSessionId

        with ex ->
            Task.FromResult(CaseFinalizeSettlement.unknown delegateSessionId ex.Message)

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

            let terminalToResult (outcome: Wanxiangshu.OpenCode.TerminalOutcome) =
                match outcome with
                | Wanxiangshu.OpenCode.TerminalOutcome.Completed _ -> Ok()
                | Wanxiangshu.OpenCode.TerminalOutcome.Failed stop -> Error stop.Reason
                | Wanxiangshu.OpenCode.TerminalOutcome.Aborted stop -> Error stop.Reason

            let sendResultToUnit (outcome: Wanxiangshu.Foundation.Outcome.SendOutcome) =
                match outcome with
                | Wanxiangshu.Foundation.Outcome.SendOutcome.AdmittedWithReceipt _
                | Wanxiangshu.Foundation.Outcome.SendOutcome.AdmittedWithPhysicalMessage _ -> Ok()
                | Wanxiangshu.Foundation.Outcome.SendOutcome.Retryable r
                | Wanxiangshu.Foundation.Outcome.SendOutcome.Fatal r
                | Wanxiangshu.Foundation.Outcome.SendOutcome.AcceptanceUnknown r -> Error r

            let completeHost
                eventPort
                (sessionPort: ISessionHostPort)
                snapshotOpt
                terminalKey
                sharedTerminalPort
                : Task<Host> =
                task {
                    match boot.Journal with
                    | Some journal ->
                        let kbPort =
                            { new Wanxiangshu.Repository.Knowledge.Casebook.ICasebookSessionPort with
                                member _.AbortSession childId = sessionPort.AbortSession childId

                                member _.SubscribeTerminal(childId, listener) =
                                    sessionPort.SubscribeTerminal(
                                        childId,
                                        (fun id outcome -> listener id (terminalToResult outcome))
                                    )

                                member _.SendPrompt(childId, text, agent) =
                                    task {
                                        let exactTools = Map.ofList [ "*", false; "js-bookkeeper", true ]

                                        let opts: Wanxiangshu.OpenCode.SessionPromptOptions =
                                            { Model = None
                                              Agent = Some agent
                                              Directory = None
                                              Metadata = None
                                              Tools = Some exactTools
                                              BindingIntent = Wanxiangshu.OpenCode.SessionBindingIntent.Preserve
                                              DetachedListener = None }

                                        let! outcome = sessionPort.SendPrompt(childId, text, opts)
                                        return sendResultToUnit outcome
                                    }

                                member _.CreateSiblingSession(owner, title, agent) =
                                    sessionPort.CreateSiblingSession(
                                        owner,
                                        None,
                                        { Title = Some title
                                          Agent = Some agent
                                          Directory = None }
                                    ) }

                        BookkeeperRuntime.setPort kbPort (fun ownerSessionId ->
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
                            (Some tryFinalize)
                            (Some CasebookLifecycle.cleanupDraft)

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

            // Continue keeps the authority run alive while its former physical
            // attempt must still be interrupted. A new root after Accepted is different.
            let isLifecycleTerminated (sessionId: SessionId) =
                let sessionOpt =
                    boot.Journal
                    |> Option.bind (fun durable ->
                        let snapshot = AgentJournal.snapshot durable
                        AgentProjection.tryFind sessionId snapshot.AgentProjections)

                let activeRoot =
                    sessionOpt
                    |> Option.bind (fun s -> s.PromptAuthority)
                    |> Option.bind (fun pa -> pa.ActiveLogicalRun)
                    |> Option.map (fun profile -> AuthorityRootUserMessageId.value profile.AuthorityRootUserMessageId)

                sessionOpt
                |> Option.bind (fun s -> s.Relay)
                |> Option.bind (fun r -> Fold.view r (RoadId.create (SessionId.value sessionId)))
                |> Option.exists (fun road ->
                    road.LatestRetirement.IsSome
                    && (match activeRoot with
                        | None -> true
                        | Some root ->
                            road.AuthorityMessageIds
                            |> List.exists (fun oldRoot -> PhysicalUserMessageId.value oldRoot = root)))

            match PluginHost.createHost input boot.PortOpt (Some boot.FamilyParent) (Some isLifecycleTerminated) with
            | Error err -> return raise (InvalidOperationException err)
            | Ok(eventPort, sessionPort, snapshotOpt, terminalKey, sharedTerminalPort) ->
                return! completeHost eventPort sessionPort snapshotOpt terminalKey sharedTerminalPort
        }
