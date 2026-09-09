namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Git
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

    val create: observeTurnWorkflowFor: ObserveTurnWorkflowSupplier -> boot: PluginBoot.Boot -> Task<Host>
