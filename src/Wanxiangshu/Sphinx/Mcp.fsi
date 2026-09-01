namespace Wanxiangshu.Sphinx

module SphinxMcp =
    val serverName: string
    val permissionKey: string
    val toolPrefix: string
    val relativeServerEntry: string
    val isTool: name: string -> bool
    val localCommand: entryPath: string -> string array
    val fixtureCommand: fixturePath: string -> string array
