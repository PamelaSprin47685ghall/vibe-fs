module VibeFs.Kernel.McpConfig

let private repo = "https://github.com/vibheksoni/stealth-browser-mcp.git"

let stealthBrowserMcpRef (envValue: string) : string =
    if envValue = "" then "master" else envValue

let getStealthBrowserMcpCommand (envValue: string) : string =
    $"uvx --python 3.13 --from git+{repo}@{stealthBrowserMcpRef envValue} python -m server"

let getStealthBrowserMcpLocalConfig (envValue: string) : {| ``type``: string; command: string array |} =
    {| ``type`` = "local"
       command =
        [| "uvx"; "--python"; "3.13"; "--from"
           $"git+{repo}@{stealthBrowserMcpRef envValue}"; "python"; "-m"; "server" |] |}
