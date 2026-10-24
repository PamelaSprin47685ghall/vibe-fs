namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Ports

module OpenCodeHostPorts =

    /// Broadcast a neutral host signal to all active subscribers.
    val publishSignal: signal: HostSignal -> Task

    /// Build the host ports for the OpenCode adapter. The live config holder is
    /// passed in so that the adapter (the `config` hook) and the product share
    /// one `IHostConfigPort` instance over the host's live config object.
    val create: boot: PluginBoot.Boot -> host: PluginHostWiring.Host -> liveConfig: LiveHostConfig -> Task<HostPorts>
