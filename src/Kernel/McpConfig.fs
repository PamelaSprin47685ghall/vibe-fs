module VibeFs.Kernel.McpConfig

open Fable.Core

open Fable.Core.JsInterop

[<Global("process")>]
let private nodeProcess : obj = jsNative

/// Read an environment variable, returning "" when unset.
let private env (name: string) : string =
    let v = nodeProcess?env?(name)
    if isNull v then "" else string v

let stealthBrowserMcpRef () : string =
    let value = env "STEALTH_BROWSER_MCP_REF"
    if value = "" then "master" else value

let private repo = "https://github.com/vibheksoni/stealth-browser-mcp.git"

let getStealthBrowserMcpCommand () : string =
    $"uvx --python 3.13 --from git+{repo}@{stealthBrowserMcpRef ()} python -m server"

let getStealthBrowserMcpLocalConfig () : {| ``type``: string; command: string array |} =
    {| ``type`` = "local"
       command =
        [| "uvx"; "--python"; "3.13"; "--from"
           $"git+{repo}@{stealthBrowserMcpRef ()}"; "python"; "-m"; "server" |] |}
