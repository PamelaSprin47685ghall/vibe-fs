namespace Wanxiangshu.OpenCode

open System
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

/// AGENT-026: env → launch decision → Host `config.mcp.stealth-browser-mcp`.
module StealthBrowserMcpConfig =

    let private envValue (read: string -> string option) (name: string) =
        match read name with
        | Some value -> value.Trim()
        | None -> ""

    let private isTruthy (value: string) =
        match value.ToLowerInvariant() with
        | "1"
        | "true"
        | "yes" -> true
        | _ -> false

    let launchFrom (read: string -> string option) : McpLaunch =
        let disabled = envValue read "STEALTH_BROWSER_MCP_DISABLED"
        let fixture = envValue read "STEALTH_BROWSER_MCP_FIXTURE"
        let testMode = envValue read "WANXIANGSHU_TEST"
        let gitRef = envValue read "STEALTH_BROWSER_MCP_REF"

        if isTruthy disabled then
            McpLaunch.Disabled
        elif fixture <> "" then
            McpLaunch.Fixture fixture
        elif isTruthy testMode then
            McpLaunch.Disabled
        else
            McpLaunch.Uvx(if gitRef = "" then StealthBrowserMcp.defaultRef else gitRef)

    let launchFromVars (vars: obj) : McpLaunch =
        launchFrom (fun name ->
            if isNull vars then
                None
            else
                let value = vars?(name)

                if isNull value then
                    None
                else
                    let text = string value
                    if String.IsNullOrWhiteSpace text then None else Some text)

    let launchFromEnvironment () : McpLaunch =
        launchFrom (fun name ->
            match Environment.GetEnvironmentVariable name with
            | null
            | "" -> None
            | value -> Some value)

    let apply (config: obj) (launch: McpLaunch) : unit =
        if isNull config then
            ()
        else
            let mcp =
                if isNull config?mcp then
                    let created: obj = createEmpty
                    config?mcp <- created
                    created
                else
                    config?mcp

            let write (command: string array) (enabled: bool) =
                mcp?(StealthBrowserMcp.serverName) <-
                    createObj [ "type" ==> "local"; "command" ==> command; "enabled" ==> enabled ]

            match launch with
            | McpLaunch.Disabled -> write (StealthBrowserMcp.uvxCommand StealthBrowserMcp.defaultRef) false
            | McpLaunch.Fixture path -> write (StealthBrowserMcp.fixtureCommand path) true
            | McpLaunch.Uvx gitRef -> write (StealthBrowserMcp.uvxCommand gitRef) true
