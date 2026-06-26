module Wanxiangshu.Kernel.Config

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolPermission

let private repo = "https://github.com/vibheksoni/stealth-browser-mcp.git"

let stealthBrowserMcpRef (envValue: string) : string =
    if envValue = "" then "master" else envValue

let getStealthBrowserMcpCommand (envValue: string) : string =
    $"uvx --python 3.13 --from git+{repo}@{stealthBrowserMcpRef envValue} python -m server"

let getStealthBrowserMcpLocalConfig
    (envValue: string)
    : {| ``type``: string
         command: string array |}
    =
    {| ``type`` = "local"
       command =
        [| "uvx"
           "--python"
           "3.13"
           "--from"
           $"git+{repo}@{stealthBrowserMcpRef envValue}"
           "python"
           "-m"
           "server" |] |}

type Agent = ToolPermission.Agent
type Tool = ToolPermission.Tool

let canUseCanonical (agent: Agent) (tool: Tool) : bool = canUse agent tool

let canUseForHost (host: Host) (agent: Agent) (tool: Tool) : bool =
    ToolPermission.canUseForHost host agent tool

let canUse (agent: Agent) (tool: Tool) : bool = canUseForHost opencode agent tool

let deniedToolsForHost (host: Host) (agent: Agent) (tools: Tool seq) : Tool list =
    ToolPermission.deniedToolsForHost host agent tools

let deniedTools (agent: Agent) (tools: Tool seq) : Tool list = deniedToolsForHost opencode agent tools