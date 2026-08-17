namespace Wanxiangshu.OpenCode

open Fable.Core.JsInterop
open Wanxiangshu.Sphinx

/// JS-native boundary for HOST-BOUNDARY-017 Sphinx MCP launch contract tests.
/// Translates env vars → plain JS `{ kind, path, enabled, reason }` and applies
/// the launch decision to a Host config object.  No F# DU cases cross the edge.
module SphinxMcpConfigSurface =

    /// The server identity this adapter owns.
    let serverIdentity () : string = SphinxMcp.serverName

    /// The local command for a given entry path.
    let localCommandFor (entryPath: string) : obj =
        box (SphinxMcp.localCommand entryPath)

    /// Read env → plain JS launch decision: `{ kind, path, enabled, reason }`.
    /// `kind` is "disabled" | "fixture" | "local".
    let launchDecision (read: string -> string option) : obj =
        let launch = SphinxMcpConfig.launchFrom read
        let kind, path, enabled, reason =
            match launch with
            | SphinxMcpConfig.Launch.Disabled -> "disabled", "", false, "disabled"
            | SphinxMcpConfig.Launch.Fixture p -> "fixture", p, true, "fixture"
            | SphinxMcpConfig.Launch.Local p -> "local", p, true, "enabled"
        box {| kind = kind; path = path; enabled = enabled; reason = reason |}

    /// Apply a launch decision to a config, preserving other MCP servers.
    /// Returns the config object (mutated in place).
    let applyToConfig (config: obj) (read: string -> string option) : obj =
        if isNull config then config
        else
            SphinxMcpConfig.apply config (SphinxMcpConfig.launchFrom read)
            config

    /// Read the entry that *would* be written for a given launch decision,
    /// without mutating any config.  Returns plain JS:
    /// `{ type, command, enabled }`.
    let entryFor (read: string -> string option) : obj =
        let launch = SphinxMcpConfig.launchFrom read
        let command, enabled =
            match launch with
            | SphinxMcpConfig.Launch.Disabled -> SphinxMcp.localCommand (SphinxMcpConfig.defaultServerEntry ()), false
            | SphinxMcpConfig.Launch.Fixture p -> SphinxMcp.fixtureCommand p, true
            | SphinxMcpConfig.Launch.Local p -> SphinxMcp.localCommand p, true
        box {| ``type`` = "local"; command = box command; enabled = enabled |}
