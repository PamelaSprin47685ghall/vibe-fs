namespace Wanxiangshu.OpenCode

module SphinxMcpConfig =
    [<RequireQualifiedAccess>]
    type Launch =
        | Disabled
        | Fixture of path: string
        | Local of entryPath: string

    val defaultServerEntry: unit -> string
    val launchFrom: read: (string -> string option) -> Launch
    val launchFromVars: vars: obj -> Launch
    val launchFromEnvironment: unit -> Launch
    val apply: config: obj -> launch: Launch -> unit
    val serverIdentity: unit -> string
    val localCommandFor: entryPath: string -> string array
    val applyToConfig: config: obj -> launch: Launch -> obj
