namespace Wanxiangshu.Repository.Investigation.Semble

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

/// AGENT-027: internal Semble search. Not Host mcp. Not Strength.
module SembleMcpClient =

    let private variableText (vars: obj) name =
        if isNull vars then
            None
        else
            vars?(name)
            |> Option.ofObj
            |> Option.map string
            |> Option.filter (String.IsNullOrWhiteSpace >> not)

    let launchFromVars (vars: obj) : McpLaunch =
        SembleMcp.launchFrom (variableText vars)

    let launchFromEnvironment () : McpLaunch =
        SembleMcp.launchFrom (fun name ->
            match Environment.GetEnvironmentVariable name with
            | null
            | "" -> None
            | value -> Some value)

    let private invocation (launch: McpLaunch) : (string * string array) option =
        match launch with
        | McpLaunch.Disabled -> None
        | McpLaunch.Fixture path ->
            let cmd = SembleMcp.fixtureCommand path
            Some(cmd.[0], cmd.[1..])
        | McpLaunch.Uvx gitRef ->
            let cmd = SembleMcp.uvxCommand gitRef
            Some(cmd.[0], cmd.[1..])

    let private decodeSearchTool command args toolArgs : Task<SembleMcp.Hit list> =
        task {
            match! SembleMcpStdio.callTool command args SembleMcp.toolName toolArgs 15000 with
            | None -> return []
            | Some raw -> return SembleSearchCodec.parseToolResult raw
        }

    let private invokeSearch command args query repoPath topK : Task<SembleMcp.Hit list> =
        task {
            if String.IsNullOrWhiteSpace query || String.IsNullOrWhiteSpace repoPath then
                return []
            else
                let k = if topK < 1 then 1 else topK

                let toolArgs =
                    createObj
                        [ "query" ==> query
                          "repo" ==> repoPath
                          "top_k" ==> k
                          "max_snippet_lines" ==> SembleMcp.maxSnippetLines ]

                return! decodeSearchTool command args toolArgs
        }

    let search (launch: McpLaunch) (query: string) (repoPath: string) (topK: int) : Task<SembleMcp.Hit list> =
        task {
            match invocation launch with
            | None -> return []
            | Some(command, args) -> return! invokeSearch command args query repoPath topK
        }

    let searchFromEnvironment (query: string) (repoPath: string) (topK: int) =
        search (launchFromEnvironment ()) query repoPath topK
