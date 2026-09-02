namespace Wanxiangshu.OpenCode

module PluginSessionWiring =

    /// SyncDelegate + StrengthReplica runtimes, attached only when a durable
    /// journal exists (the sync path is what makes both runtimes meaningful).
    val attach: boot: PluginBoot.Boot -> host: PluginHostWiring.Host -> unit
