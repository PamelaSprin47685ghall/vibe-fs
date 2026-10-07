namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

/// Composition root (Wave 3): SpikePlugin only assembles the wiring modules.
/// Every concrete step — resource install, journal, scope, host ports, session
/// runtimes, transforms, hooks — lives in its own module, and
/// PluginBoot keeps the global initialization order authoritative.
module SpikePlugin =

    type internal Runtime =
        { Hooks: obj
          Scope: PluginRuntimeScope }

    val internal createRuntime: input: obj -> Task<Runtime>
    val initSpikePlugin: input: obj -> Task<obj>
