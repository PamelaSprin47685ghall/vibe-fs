namespace Wanxiangshu.Repository.Investigation

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Repository.Investigation.Semble

/// JS-native owner boundary for the internal Semble MCP resource. Launch
/// decisions and parsed hits are plain data; process transport remains behind
/// the owner and an external fixture is addressed only by its opaque path.
[<RequireQualifiedAccess>]
module RepositorySembleSurface =

    let private launchView (launch: McpLaunch) : obj =
        match launch with
        | McpLaunch.Disabled -> box {| kind = "Disabled" |}
        | McpLaunch.Fixture path -> box {| kind = "Fixture"; value = path |}
        | McpLaunch.Uvx gitRef -> box {| kind = "Uvx"; value = gitRef |}

    let private hitView (hit: SembleMcp.Hit) : obj =
        box
            {| filePath = hit.FilePath
               startLine = hit.StartLine
               endLine = hit.EndLine
               content = hit.Content
               score = hit.Score
               totalLines = hit.TotalLines |}

    let private launchOf (value: obj) : McpLaunch =
        let kind = if isNull value then "Disabled" else string (value?kind)

        let payload =
            if isNull value || isNull (value?value) then
                ""
            else
                string (value?value)

        match kind with
        | "Fixture" -> McpLaunch.Fixture payload
        | "Uvx" -> McpLaunch.Uvx payload
        | _ -> McpLaunch.Disabled

    let serverName = SembleMcp.serverName
    let defaultRef = SembleMcp.defaultRef
    let repo = SembleMcp.repo
    let toolName = SembleMcp.toolName
    let maxSnippetLines = SembleMcp.maxSnippetLines

    let uvxCommand (gitRef: string) : string array = SembleMcp.uvxCommand gitRef
    let fixtureCommand (fixturePath: string) : string array = SembleMcp.fixtureCommand fixturePath

    let launchFromVars (vars: obj) : obj =
        SembleMcpClient.launchFromVars vars |> launchView

    let parseText (text: string) : obj array =
        SembleSearchCodec.parseText text |> List.map hitView |> List.toArray

    let parseToolResult (result: obj) : obj array =
        SembleSearchCodec.parseToolResult result |> List.map hitView |> List.toArray

    let search (launch: obj) (query: string) (repoPath: string) (topK: int) : Task<obj array> =
        task {
            let! hits = SembleMcpClient.search (launchOf launch) query repoPath topK
            return hits |> List.map hitView |> List.toArray
        }
