namespace Wanxiangshu.OpenCode
open Wanxiangshu.Foundation

open System

/// AGENT-026: Browser-only stealth-browser MCP identity and launch command.
/// No env, no Host objects.
module StealthBrowserMcp =

    let serverName = "stealth-browser-mcp"
    let permissionKey = "stealth-browser-mcp_*"
    let toolPrefix = "stealth-browser-mcp_"
    let defaultRef = "master"
    let repo = "https://github.com/vibheksoni/stealth-browser-mcp.git"

    let isTool (name: string) =
        not (String.IsNullOrWhiteSpace name) && name.StartsWith toolPrefix

    let uvxCommand (gitRef: string) : string array =
        let refValue =
            if String.IsNullOrWhiteSpace gitRef then
                defaultRef
            else
                gitRef.Trim()

        [| "uvx"
           "--python"
           "3.13"
           "--from"
           sprintf "git+%s@%s" repo refValue
           "python"
           "-m"
           "server" |]

    let fixtureCommand (fixturePath: string) : string array = [| "node"; fixturePath |]
