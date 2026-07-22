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

let extractImports (content: string) : string list =
    if isNull content then
        []
    else
        let lines = content.Split('\n')
        let mutable inImport = false
        let mutable imports = []

        for rawLine in lines do
            let line = rawLine.Trim()

            if line.StartsWith "import:" then
                inImport <- true
            elif inImport && line.StartsWith "-" then
                let item = line.Substring(1).Trim()
                if item <> "" then imports <- item :: imports
            elif inImport && (line.StartsWith "---" || (line.Contains ":" && not (line.StartsWith "-"))) then
                inImport <- false

        List.rev imports

let findCapsFiles (projectRoot: string) : JS.Promise<CapsFile list> =
    promise {
        let agentsPath = joinPath projectRoot "AGENTS.md"
        let! agentsFile = tryReadFileAsync agentsPath "AGENTS.md"

        match agentsFile with
        | None -> return []
        | Some agents ->
            let initial =
                if System.String.IsNullOrWhiteSpace agents.content then
                    fresh ()
                else
                    absorb
                        { filePath = agentsPath
                          label = "AGENTS.md"
                          content = agents.content }
                        (fresh ())

            let imports = extractImports agents.content

            let! withImports =
                if List.isEmpty imports then
                    Promise.lift initial
                else
                    readImportsAsync projectRoot imports initial

            return withImports.results |> Seq.toList |> List.sortBy (fun file -> file.filePath)
    }
