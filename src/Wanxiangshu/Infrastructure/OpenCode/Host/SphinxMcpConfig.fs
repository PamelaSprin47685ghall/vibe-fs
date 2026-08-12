namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel

/// AGENT-028: env → launch decision → Host `config.mcp.sphinx`.
module SphinxMcpConfig =

    /// Sphinx-only launch (local node entry). Not shared McpLaunch — avoids uvx case pollution.
    [<RequireQualifiedAccess>]
    type Launch =
        | Disabled
        | Fixture of path: string
        | Local of entryPath: string

    [<Import("fileURLToPath", "node:url")>]
    let private fileURLToPath (url: string) : string = jsNative

    [<Import("dirname", "node:path")>]
    let private dirname (path: string) : string = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string, b: string) : string = jsNative

    [<Emit("import.meta.url")>]
    let private importMetaUrl: string = jsNative

    /// Compiled module: dist/Infrastructure/OpenCode/Host/ → four levels to package root.
    let private packageRoot () =
        let here = dirname (fileURLToPath importMetaUrl)
        pathJoin (pathJoin (pathJoin (pathJoin (here, ".."), ".."), ".."), "..")

    let defaultServerEntry () : string =
        pathJoin (pathJoin (pathJoin (packageRoot (), "dist"), "sphinx"), "mcp-server.js")

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

    let launchFrom (read: string -> string option) : Launch =
        let disabled = envValue read "SPHINX_MCP_DISABLED"
        let fixture = envValue read "SPHINX_MCP_FIXTURE"
        let testMode = envValue read "WANXIANGSHU_TEST"

        if isTruthy disabled then Launch.Disabled
        elif fixture <> "" then Launch.Fixture fixture
        elif isTruthy testMode then Launch.Disabled
        else Launch.Local(defaultServerEntry ())

    let launchFromVars (vars: obj) : Launch =
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

    let launchFromEnvironment () : Launch =
        launchFrom (fun name ->
            match Environment.GetEnvironmentVariable name with
            | null
            | "" -> None
            | value -> Some value)

    let apply (config: obj) (launch: Launch) : unit =
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
                mcp?(SphinxMcp.serverName) <-
                    createObj [ "type" ==> "local"; "command" ==> command; "enabled" ==> enabled ]

            match launch with
            | Launch.Disabled -> write (SphinxMcp.localCommand (defaultServerEntry ())) false
            | Launch.Fixture path -> write (SphinxMcp.fixtureCommand path) true
            | Launch.Local entryPath -> write (SphinxMcp.localCommand entryPath) true
