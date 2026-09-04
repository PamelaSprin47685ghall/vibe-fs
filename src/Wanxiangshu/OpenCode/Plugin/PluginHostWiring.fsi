namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
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
          StrengthDurability: StrengthDurabilityPort option }

    val create: boot: PluginBoot.Boot -> Task<Host>
