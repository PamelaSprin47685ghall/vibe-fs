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
          GitTreePort: Wanxiangshu.Review.GitTreePort option
          StrengthDurability: StrengthDurabilityPort option }

    let create (boot: PluginBoot.Boot) : Task<Host> =
        task {
            let input = boot.Input
            let scope = boot.Scope

            match PluginHost.createHost input boot.PortOpt (Some boot.FamilyParent) with
            | Error err -> return raise (InvalidOperationException err)
            | Ok(eventPort, sessionPort, snapshotOpt, terminalKey, sharedTerminalPort) ->
                BookkeeperRuntime.setSessionPort sessionPort
                scope.AttachSharedTerminal(terminalKey, sharedTerminalPort)
                scope.AttachSatelliteRuntime(SatelliteRuntime sessionPort)

                for KeyValue(childId, parentId) in scope.Sessions.SessionParents do
                    scope.Sessions.OwnedSessions.Add childId |> ignore
                    scope.Sessions.OwnedSessions.Add parentId |> ignore

                let workspaceDirectory = boot.WorkspaceDirectory

                // STRENGTH-006..008: borrow the same unified EventStore already
                // acquired by AgentJournal boot. Keep the handle in the composition
                // root rather than PluginRuntimeScope so Journal and EventStore
                // writers never become one dual-write owner.
                let strengthDurability =
                    match boot.Journal, workspaceDirectory with
                    | Some _, Some workspace ->
                        WorkspaceEventStore.tryCurrent (RuntimePath.gitCommonDir workspace)
                        |> Option.map StrengthDurability.create
                    | _ -> None

                // Causal wait bridge must stay on the root workspace so E2E
                // diagnostics (host.workDir) can read active waits. Later worktree
                // plugin boots must not redirect the process-local hub.
                if SharedState.RootWorkspace.IsNone then
                    SharedState.RootWorkspace <- workspaceDirectory
                    CausalWaitHub.setWorkspace workspaceDirectory

                // CASE-003/010: CasebookLifecycle collector enablement (marker-gated).
                // SpikePlugin only Collects / enables — store IO stays in Lifecycle.
                CasebookLifecycle.setEnabled workspaceDirectory

                let! wired =
                    HostSignalBootstrap.wire
                        sessionPort
                        eventPort
                        snapshotOpt
                        boot.Journal
                        strengthDurability
                        scope
                        input
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
                      GitTreePort = boot.GitTreePort
                      StrengthDurability = strengthDurability }
        }
