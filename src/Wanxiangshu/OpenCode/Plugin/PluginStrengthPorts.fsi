namespace Wanxiangshu.OpenCode

open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

/// Neutral strength closures for the Host boundary, built once by plugin
/// composition from the already-held scope and durability handle.
///
/// The Host boundary consumes only `HostSignalBootstrap.StrengthHostPorts`;
/// all Strength knowledge (replica runtime, primary evidence, durable
/// promotion, fuse trips) stays behind these delegates.
module PluginStrengthPorts =

    val create:
        strengthScope: PluginStrengthScope option ->
        strengthDurability: StrengthDurabilityPort option ->
            HostSignalBootstrap.StrengthHostPorts
