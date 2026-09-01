namespace Wanxiangshu.OpenCode

module StealthBrowserMcp =
    val serverName: string
    val permissionKey: string
    val toolPrefix: string
    val defaultRef: string
    val repo: string
    val isTool: name: string -> bool
    val uvxCommand: gitRef: string -> string array
    val fixtureCommand: fixturePath: string -> string array
