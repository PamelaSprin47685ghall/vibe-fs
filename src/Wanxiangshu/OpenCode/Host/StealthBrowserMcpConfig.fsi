namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation

module StealthBrowserMcpConfig =
    val launchFrom: read: (string -> string option) -> McpLaunch
    val launchFromVars: vars: obj -> McpLaunch
    val launchFromEnvironment: unit -> McpLaunch
    val apply: config: obj -> launch: McpLaunch -> unit
