namespace Wanxiangshu.OpenCode

module SphinxMcpConfigSurface =
    val serverIdentity: unit -> string
    val localCommandFor: entryPath: string -> obj
    val launchDecision: read: (string -> string option) -> obj
    val applyToConfig: config: obj -> read: (string -> string option) -> obj
    val entryFor: read: (string -> string option) -> obj
