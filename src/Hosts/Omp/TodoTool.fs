module Wanxiangshu.Hosts.Omp.TodoTool

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Runtime.WorkBacklogToolsCodec
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.OmpToolSchema
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime

module Dyn = Wanxiangshu.Runtime.Dyn

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

let private isAllCompleted (todos: TodoItem array) : bool =
    todos
    |> Array.forall (fun t ->
        match t.Status with
        | Wanxiangshu.Kernel.ToolArgs.Completed
        | Wanxiangshu.Kernel.ToolArgs.Cancelled -> true
        | _ -> false)

let private parseTodos (params': obj) (parseError: string option ref) : TodoItem array =
    let raw = Dyn.get params' "todos"

    if Dyn.isNullish raw || not (Dyn.isArray raw) then
        [||]
    else
        unbox<obj array> raw
        |> Array.map (fun item ->
            let statusStr = Dyn.str item "status"
            let priorityStr = Dyn.str item "priority"

            let status =
                match Wanxiangshu.Runtime.WorkBacklogToolsCodec.parseTodoItemStatus statusStr with
                | Ok s -> s
                | Error _ ->
                    parseError.Value <- Some $"Invalid todo status: %s{statusStr}"
                    Wanxiangshu.Kernel.ToolArgs.Todo

            let priority =
                if System.String.IsNullOrWhiteSpace priorityStr then
                    Wanxiangshu.Kernel.ToolArgs.Low
                else
                    match Wanxiangshu.Runtime.WorkBacklogToolsCodec.parseTodoItemPriority priorityStr with
                    | Ok p -> p
                    | Error _ ->
                        parseError.Value <- Some $"Invalid todo priority: %s{priorityStr}"
                        Wanxiangshu.Kernel.ToolArgs.Low

            { Content = Dyn.str item "content"
              Status = status
              Priority = priority })

let private handleTodoCommit (ctx: obj) (args: TodoWriteArgs) : JS.Promise<unit> =
    promise {
        match getSessionIdFromContext ctx with
        | Some sid ->
            let root = Dyn.str ctx "cwd"

            if root <> "" then
                do! appendWorkBacklogCommittedOrFail root sid args

                let allCompleted = isAllCompleted args.Todos

                let ev =
                    { CurrentTurnEvidence.empty with
                        Todos = if allCompleted then TodosCompleted else TodosNotCompleted }

                do! SubsessionEventRouter.routeEvidence root sid ev |> Promise.map ignore
        | None -> ()
    }

let private generateOutput (params': obj) (methodologies: string list) : string =
    let baseOutput = todoWriteOutput methodologies

    let violations =
        match Wanxiangshu.Runtime.WorkBacklogToolsCodec.decodeTodoWriteArgs false params' with
        | Ok(_, viols) -> viols
        | Error _ -> []

    if not violations.IsEmpty then
        Wanxiangshu.Runtime.ToolHookRuntime.appendCriticism
            baseOutput
            violations
            Wanxiangshu.Runtime.ToolHookRuntime.ExecutionStatus.Success
    else
        baseOutput

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

                      if methodologies.IsEmpty then
                          return errorResult "todowrite requires select_methodology"
                      else
                          match validateTodos params' with
                          | Error msg -> return errorResult msg
                          | Ok() ->
                              let parseError = ref None

                              let todos = parseTodos params' parseError

                              match parseError.Value with
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

                                  do! handleTodoCommit ctx args

                                  return textResult (generateOutput params' methodologies)
                  }) ]
    )
