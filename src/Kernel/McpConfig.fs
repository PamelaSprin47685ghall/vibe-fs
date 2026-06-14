module VibeFs.Kernel.McpConfig

open Fable.Core

/// Read an environment variable, returning "" when unset.
[<Emit("process.env[$0] ?? ''")>]
let private env (_name: string) : string = jsNative

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
