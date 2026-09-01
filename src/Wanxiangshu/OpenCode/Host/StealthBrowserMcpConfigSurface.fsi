namespace Wanxiangshu.OpenCode

module StealthBrowserMcpConfigSurface =
    val serverIdentity: unit -> string
    val uvxCommandFor: gitRef: string -> obj
    val fixtureCommandFor: path: string -> obj
    val launchDecision: read: (string -> string option) -> obj
    val applyToConfig: config: obj -> read: (string -> string option) -> obj
    val entryFor: read: (string -> string option) -> obj
