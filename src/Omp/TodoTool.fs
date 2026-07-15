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
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Shell

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

                      if methodologies.IsEmpty then
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

                                          let allCompleted =
                                              args.Todos
                                              |> Array.forall (fun t ->
                                                  match t.Status with
                                                  | Wanxiangshu.Kernel.ToolArgs.TodoItemStatus.Completed
                                                  | Wanxiangshu.Kernel.ToolArgs.TodoItemStatus.Cancelled -> true
                                                  | _ -> false)

                                          let ev =
                                              { CurrentTurnEvidence.empty with
                                                  Todos = if allCompleted then TodosCompleted else TodosNotCompleted }

                                          do!
                                              SubsessionEventRouter.routeToChild
                                                  sid
                                                  (EvidenceUpdated { TurnId = None; Evidence = ev })
                                              |> Promise.map ignore
                                  | None -> ()

                                  let baseOutput = todoWriteOutput methodologies

                                  let violations =
                                      match decodeTodoWriteArgs false params' with
                                      | Ok(_, viols) -> viols
                                      | Error _ -> []

                                  let finalOutput =
                                      if not violations.IsEmpty then
                                          Wanxiangshu.Shell.ToolHookRuntime.appendCriticism
                                              baseOutput
                                              violations
                                              Wanxiangshu.Shell.ToolHookRuntime.ExecutionStatus.Success
                                      else
                                          baseOutput

                                  return textResult finalOutput
                  }) ]
    )
