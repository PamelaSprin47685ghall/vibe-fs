namespace Wanxiangshu.Repository.Investigation.Semble

open Wanxiangshu.Change
open Wanxiangshu.Enforcer
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Resources
open Wanxiangshu.Strength.Persistence

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

/// AGENT-027: internal Semble search. Not Host mcp. Not Strength.
module SembleMcpClient =

    let launchFromVars (vars: obj) : McpLaunch =
        SembleMcp.launchFrom (fun name ->
            if isNull vars then
                None
            else
                let value = vars?(name)

                if isNull value then
                    None
                else
                    let text = string value
                    if String.IsNullOrWhiteSpace text then None else Some text)

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

    let search (launch: McpLaunch) (query: string) (repoPath: string) (topK: int) : Task<SembleMcp.Hit list> =
        task {
            match invocation launch with
            | None -> return []
            | Some(command, args) ->
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

                    match! SembleMcpStdio.callTool command args SembleMcp.toolName toolArgs 15000 with
                    | None -> return []
                    | Some raw -> return SembleSearchCodec.parseToolResult raw
        }

    let searchFromEnvironment (query: string) (repoPath: string) (topK: int) =
        search (launchFromEnvironment ()) query repoPath topK
