namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System.Threading.Tasks

/// Composition root (Wave 3): SpikePlugin only assembles the wiring modules.
/// Every concrete step — resource install, journal, scope, host ports, session
/// runtimes, recovery ports, transforms, hooks — lives in its own module, and
/// PluginBoot keeps the global initialization order authoritative.
module SpikePlugin =

    let initSpikePlugin (input: obj) : Task<obj> =
        task {
            let boot = PluginBoot.create input
            let! host = PluginHostWiring.create boot
            PluginSessionWiring.attach boot host
            PluginRecoveryWiring.attach boot host
            let transform = PluginTransforms.create boot host
            return! PluginHooks.create boot host transform
        }
