namespace Wanxiangshu.Repository.Investigation.Semble

open Wanxiangshu.Foundation
open System

/// AGENT-027: internal Semble MCP identity, launch command, and search hit.
/// Not Host-wired. No env I/O.
module SembleMcp =

    let serverName = "semble"
    let defaultRef = "main"
    let repo = "https://github.com/MinishLab/semble.git"
    let toolName = "search"
    let maxSnippetLines = 20

    type Hit =
        { FilePath: string
          StartLine: int
          EndLine: int
          Content: string
          Score: float
          TotalLines: int }

    let uvxCommand (gitRef: string) : string array =
        let refValue =
            if String.IsNullOrWhiteSpace gitRef then
                defaultRef
            else
                gitRef.Trim()

        [| "uvx"; "--from"; sprintf "semble[mcp] @ git+%s@%s" repo refValue; "semble" |]

    let fixtureCommand (fixturePath: string) : string array = [| "node"; fixturePath |]

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
        let disabled = envValue read "SEMBLE_MCP_DISABLED"
        let fixture = envValue read "SEMBLE_MCP_FIXTURE"
        let testMode = envValue read "WANXIANGSHU_TEST"
        let gitRef = envValue read "SEMBLE_MCP_REF"

        if isTruthy disabled then
            McpLaunch.Disabled
        elif fixture <> "" then
            McpLaunch.Fixture fixture
        elif isTruthy testMode then
            McpLaunch.Disabled
        else
            McpLaunch.Uvx(if gitRef = "" then defaultRef else gitRef)
