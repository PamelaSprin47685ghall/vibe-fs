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

            return initial.results |> Seq.toList |> List.sortBy (fun file -> file.filePath)
    }
