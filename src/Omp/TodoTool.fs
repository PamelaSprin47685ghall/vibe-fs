module Wanxiangshu.Omp.TodoTool

open Fable.Core.JsInterop
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.OmpToolSchema
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.WorkBacklogToolsCodec

module Dyn = Wanxiangshu.Shell.Dyn

let private decodeMethodologies (params': obj) : string list =
    let raw = Dyn.get params' "select_methodology"

    if Dyn.isNullish raw || not (Dyn.isArray raw) then
        []
    else
        unbox<obj array> raw |> Array.map string |> Array.toList

let private validateTodos (params': obj) : Result<unit, string> =
    let raw = Dyn.get params' "todos"

    if Dyn.isNullish raw || not (Dyn.isArray raw) then
        Error "todowrite requires a todos array"
    else
        let todos = unbox<obj array> raw

        todos
        |> Array.tryFindIndex (fun item ->
            let content = (Dyn.str item "content").Trim()
            let status = (Dyn.str item "status").Trim()
            content = "" || status = "")
        |> function
            | Some i -> Error $"todowrite todos[{i}] requires content and status"
            | None -> Ok()

let registerTodoTool (pi: obj) : unit =
    let tb = Dyn.get pi "typebox"

    pi?registerTool (
        createObj
            [ "name", box "todowrite"
              "label", box "Todo Write"
              "description", box (toolDescriptionFor omp)
              "parameters", todowriteParameters tb
              "execute",
              box (fun (_id: string) (params': obj) (_s: obj) (_u: obj) (ctx: obj) ->
                  promise {
                      let ahaMoments = (Dyn.str params' "ahaMoments").Trim()
                      let changesAndReasons = (Dyn.str params' "changesAndReasons").Trim()
                      let gotchas = (Dyn.str params' "gotchas").Trim()
                      let lessonsAndConventions = (Dyn.str params' "lessonsAndConventions").Trim()
                      let plan = (Dyn.str params' "plan").Trim()
                      let methodologies = decodeMethodologies params'

                      if ahaMoments.Length < 1024 then
                          return errorResult "todowrite requires ahaMoments (min 1024 chars)"
                      elif changesAndReasons.Length < 1024 then
                          return errorResult "todowrite requires changesAndReasons (min 1024 chars)"
                      elif gotchas.Length < 1024 then
                          return errorResult "todowrite requires gotchas (min 1024 chars)"
                      elif lessonsAndConventions.Length < 1024 then
                          return errorResult "todowrite requires lessonsAndConventions (min 1024 chars)"
                      elif plan.Length < 1024 then
                          return errorResult "todowrite requires plan (min 1024 chars)"
                      elif methodologies.IsEmpty then
                          return errorResult "todowrite requires select_methodology"
                      else
                          match validateTodos params' with
                          | Error msg -> return errorResult msg
                          | Ok() ->
                              let mutable parseError = None

                              let todos =
                                  let raw = Dyn.get params' "todos"

                                  if Dyn.isNullish raw || not (Dyn.isArray raw) then
                                      [||]
                                  else
                                      unbox<obj array> raw
                                      |> Array.map (fun item ->
                                          let statusStr = Dyn.str item "status"
                                          let priorityStr = Dyn.str item "priority"

                                          let status =
                                              match parseTodoItemStatus statusStr with
                                              | Ok s -> s
                                              | Error _ ->
                                                  parseError <- Some $"Invalid todo status: %s{statusStr}"
                                                  Wanxiangshu.Kernel.ToolArgs.TodoItemStatus.Todo

                                          let priority =
                                              if System.String.IsNullOrWhiteSpace priorityStr then
                                                  Wanxiangshu.Kernel.ToolArgs.TodoItemPriority.Low
                                              else
                                                  match parseTodoItemPriority priorityStr with
                                                  | Ok p -> p
                                                  | Error _ ->
                                                      parseError <- Some $"Invalid todo priority: %s{priorityStr}"
                                                      Wanxiangshu.Kernel.ToolArgs.TodoItemPriority.Low

                                          { Content = Dyn.str item "content"
                                            Status = status
                                            Priority = priority })

                              match parseError with
                              | Some err -> return errorResult err
                              | None ->
                                  let args =
                                      { AhaMoments = ahaMoments
                                        ChangesAndReasons = changesAndReasons
                                        Gotchas = gotchas
                                        LessonsAndConventions = lessonsAndConventions
                                        Plan = plan
                                        Todos = todos
                                        SelectMethodology = methodologies }

                                  match getSessionIdFromContext ctx with
                                  | Some sid ->
                                      let root = Dyn.str ctx "cwd"

                                      if root <> "" then
                                          do! appendWorkBacklogCommittedOrFail root sid args
                                  | None -> ()

                                  return textResult (todoWriteOutput methodologies false)
                  }) ]
    )
