namespace Wanxiangshu.OpenCode

module StealthBrowserMcpConfig =
    [<RequireQualifiedAccess>]
    type Launch =
        | Disabled
        | Fixture of path: string
        | Uvx of gitRef: string

    val launchFrom: read: (string -> string option) -> Launch
    val launchFromVars: vars: obj -> Launch
    val launchFromEnvironment: unit -> Launch
    val apply: config: obj -> launch: Launch -> unit
