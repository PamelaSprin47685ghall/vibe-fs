namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

module PluginHooks =

    /// Host hook surface: chat / transform / config / compaction / text /
    /// tool hooks plus event + dispose, and the optional client tool module.
    val create:
        boot: PluginBoot.Boot -> host: PluginHostWiring.Host -> transform: (obj -> obj -> Task<unit>) -> Task<obj>
