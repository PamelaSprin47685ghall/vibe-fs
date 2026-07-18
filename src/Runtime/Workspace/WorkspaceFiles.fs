module Wanxiangshu.Runtime.WorkspaceFiles

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.WorkspacePathResolution
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.WorkspaceFilesCollect

let maxFileSize = WorkspaceFilesCollect.maxFileSize

type Budget = WorkspaceFilesCollect.Budget

let fresh () = WorkspaceFilesCollect.fresh ()

let isFull budget = WorkspaceFilesCollect.isFull budget

let absorb file budget =
    WorkspaceFilesCollect.absorb file budget

[<Import("parse", "yaml")>]
let private yamlParse (text: string) : obj = jsNative

let private splitFrontMatter (content: string) : string * obj option =
    let trimmed = content.TrimStart('\r', '\n')

    if not (trimmed.StartsWith("---")) then
        (content, None)
    else
        let afterFirst = trimmed.[3..].TrimStart('\r', '\n')

        match afterFirst.IndexOf("---") with
        | -1 -> (content, None)
        | closeIdx ->
            let yamlText = afterFirst.[.. closeIdx - 1]
            let body = afterFirst.[closeIdx + 3 ..].TrimStart('\r', '\n')

            let fm =
                try
                    Some(yamlParse yamlText)
                with _ ->
                    None

            (body, fm)

let private extractImportList (frontmatter: obj option) : string list =
    match frontmatter with
    | None -> []
    | Some fm ->
        if Dyn.isNullish fm then
            []
        else
            let importVal = Dyn.get fm "import"

            if Dyn.isNullish importVal then
                []
            elif Dyn.isArray importVal then
                importVal :?> obj array |> Array.map string |> List.ofArray
            else
                [ string importVal ]

let findCapsFiles (projectRoot: string) : JS.Promise<CapsFile list> =
    promise {
        let agentsPath = joinPath projectRoot "AGENTS.md"
        let! agentsFile = tryReadFileAsync agentsPath "AGENTS.md"

        match agentsFile with
        | None -> return []
        | Some agents ->
            let body, fm = splitFrontMatter agents.content
            let importList = extractImportList fm

            let initial =
                if System.String.IsNullOrWhiteSpace body then
                    fresh ()
                else
                    absorb
                        { filePath = agentsPath
                          label = "AGENTS.md"
                          content = body }
                        (fresh ())

            let! finalBudget = readImportsAsync projectRoot importList initial
            return finalBudget.results |> Seq.toList |> List.sortBy (fun file -> file.filePath)
    }
