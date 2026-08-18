namespace Wanxiangshu.OpenCode

open Fable.Core.JsInterop
open Wanxiangshu.Foundation

/// JS-native boundary for HOST-BOUNDARY-017 stealth-browser MCP launch contract
/// tests.  Translates env vars → plain JS `{ kind, ref, enabled, reason }` and
/// applies the launch decision to a Host config object.  No F# DU cases cross
/// the edge.
module StealthBrowserMcpConfigSurface =

    /// The server identity this adapter owns.
    let serverIdentity () : string = StealthBrowserMcp.serverName

    /// The uvx command for a given git ref.
    let uvxCommandFor (gitRef: string) : obj =
        box (StealthBrowserMcp.uvxCommand gitRef)

    /// The fixture command for a given path.
    let fixtureCommandFor (path: string) : obj =
        box (StealthBrowserMcp.fixtureCommand path)

    /// Read env → plain JS launch decision: `{ kind, ref, path, enabled, reason }`.
    /// `kind` is "disabled" | "fixture" | "uvx".
    let launchDecision (read: string -> string option) : obj =
        let launch = StealthBrowserMcpConfig.launchFrom read

        let kind, ref, path, enabled, reason =
            match launch with
            | McpLaunch.Disabled -> "disabled", "", "", false, "disabled"
            | McpLaunch.Fixture p -> "fixture", "", p, true, "fixture"
            | McpLaunch.Uvx r -> "uvx", r, "", true, "enabled"

        box
            {| kind = kind
               ref = ref
               path = path
               enabled = enabled
               reason = reason |}

    /// Apply a launch decision to a config, preserving other MCP servers.
    /// Returns the config object (mutated in place).
    let applyToConfig (config: obj) (read: string -> string option) : obj =
        if isNull config then
            config
        else
            StealthBrowserMcpConfig.apply config (StealthBrowserMcpConfig.launchFrom read)
            config

    /// Read the entry that *would* be written for a given launch decision,
    /// without mutating any config.  Returns plain JS:
    /// `{ type, command, enabled }`.
    let entryFor (read: string -> string option) : obj =
        let launch = StealthBrowserMcpConfig.launchFrom read

        let command, enabled =
            match launch with
            | McpLaunch.Disabled -> StealthBrowserMcp.uvxCommand StealthBrowserMcp.defaultRef, false
            | McpLaunch.Fixture p -> StealthBrowserMcp.fixtureCommand p, true
            | McpLaunch.Uvx r -> StealthBrowserMcp.uvxCommand r, true

        box
            {| ``type`` = "local"
               command = box command
               enabled = enabled |}
