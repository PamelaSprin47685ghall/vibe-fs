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

    let private nonBlankVarValue (value: obj) =
        let text = string value
        if String.IsNullOrWhiteSpace text then None else Some text

    let private readVarValue (vars: obj) name =
        let value = vars?(name)
        if isNull value then None else nonBlankVarValue value

    let private readVar (vars: obj) name =
        if isNull vars then None else readVarValue vars name

    let launchFromVars (vars: obj) : McpLaunch = launchFrom (readVar vars)

    let launchFromEnvironment () : McpLaunch =
        launchFrom (fun name ->
            match Environment.GetEnvironmentVariable name with
            | null
            | "" -> None
            | value -> Some value)

    let private ensureMcp config =
        if isNull config?mcp then
            let created: obj = createEmpty
            config?mcp <- created
            created
        else
            config?mcp

    let private applyLaunch mcp launch =
        let write (command: string array) (enabled: bool) =
            mcp?(StealthBrowserMcp.serverName) <-
                createObj [ "type" ==> "local"; "command" ==> command; "enabled" ==> enabled ]

        match launch with
        | McpLaunch.Disabled -> write (StealthBrowserMcp.uvxCommand StealthBrowserMcp.defaultRef) false
        | McpLaunch.Fixture path -> write (StealthBrowserMcp.fixtureCommand path) true
        | McpLaunch.Uvx gitRef -> write (StealthBrowserMcp.uvxCommand gitRef) true

    let private applyNonNull config launch = applyLaunch (ensureMcp config) launch

    let apply (config: obj) (launch: McpLaunch) : unit =
        if isNull config then () else applyNonNull config launch
