module VibeFs.Omp.TodoTool

open Fable.Core.JsInterop
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.MagicTodo
open VibeFs.Kernel.Methodology
open VibeFs.Kernel.ToolOutputInfo
open VibeFs.Omp.Codec
open VibeFs.Omp.OmpToolSchema
module Dyn = VibeFs.Shell.Dyn

let private decodeMethodologies (params': obj) : string list =
    let raw = Dyn.get params' "select_methodology"
    if Dyn.isNullish raw || not (Dyn.isArray raw) then []
    else unbox<obj array> raw |> Array.map string |> Array.toList

let private validateTodos (params': obj) : Result<unit, string> =
    let raw = Dyn.get params' "todos"
    if Dyn.isNullish raw || not (Dyn.isArray raw) then Error "todowrite requires a todos array"
    else
        let todos = unbox<obj array> raw
        todos
        |> Array.tryFindIndex (fun item ->
            let content = (Dyn.str item "content").Trim()
            let status = (Dyn.str item "status").Trim()
            content = "" || status = "")
        |> function
            | Some i -> Error $"todowrite todos[{i}] requires content and status"
            | None -> Ok ()

let registerTodoTool (pi: obj) : unit =
    let tb = Dyn.get pi "typebox"
    pi?registerTool(
        createObj [
            "name", box "todowrite"
            "label", box "Todo Write"
            "description", box (toolDescriptionFor opencode)
            "parameters", todowriteParameters tb
            "execute",
                box(fun (_id: string) (params': obj) (_s: obj) (_u: obj) (_ctx: obj) ->
                    promise {
                        let report = (Dyn.str params' "completedWorkReport").Trim()
                        let methodologies = decodeMethodologies params'
                        if report = "" then return errorResult "todowrite requires completedWorkReport"
                        elif methodologies.IsEmpty then return errorResult "todowrite requires select_methodology"
                        else
                            match validateTodos params' with
                            | Error msg -> return errorResult msg
                            | Ok () -> return textResult (todoWriteOutput methodologies false)
                    })
        ])