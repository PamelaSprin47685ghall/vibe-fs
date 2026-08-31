namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System.Threading.Tasks

/// Composition root (Wave 3): SpikePlugin only assembles the wiring modules.
/// Every concrete step — resource install, journal, scope, host ports, session
/// runtimes, transforms, hooks — lives in its own module, and
/// PluginBoot keeps the global initialization order authoritative.
module SpikePlugin =

    let initSpikePlugin (input: obj) : Task<obj> =
        task {
            try
                let! boot = PluginBoot.create input
                let! host = PluginHostWiring.create boot
                PluginSessionWiring.attach boot host
                PluginRecoveryWiring.attach boot
                let transform = PluginTransforms.create boot host
                return! PluginHooks.create boot host transform
            with ex ->
                // A partially initialized Wanxiangshu instance is not a degraded
                // mode. OpenCode may otherwise keep running after a plugin-load
                // rejection with only half the runtime owners installed.
                Diagnostic.fatal "plugin-initialization-failed" [ "result", ex.Message ]
                return raise ex
        }
