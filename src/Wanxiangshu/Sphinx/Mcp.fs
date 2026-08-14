namespace Wanxiangshu.Sphinx
open Wanxiangshu.Foundation
open Wanxiangshu.OpenCode

open System

/// AGENT-030: Sphinx MCP identity and local launch command.
/// No env, no Host objects.
module SphinxMcp =

    let serverName = "sphinx"
    let permissionKey = "sphinx_*"
    let toolPrefix = "sphinx_"
    let relativeServerEntry = "dist/Sphinx/McpServer.js"

    let isTool (name: string) =
        not (String.IsNullOrWhiteSpace name) && name.StartsWith toolPrefix

    let localCommand (entryPath: string) : string array = [| "node"; entryPath |]

    let fixtureCommand (fixturePath: string) : string array = [| "node"; fixturePath |]
