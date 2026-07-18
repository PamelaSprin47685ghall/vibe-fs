module Wanxiangshu.Hosts.Mux.TodoWriteToolWrapper

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Runtime.PromptFragments
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Runtime.WorkBacklogSchema
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.SessionProjectionStore
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.MuxHostBindings
open Wanxiangshu.Runtime.WorkBacklogToolsCodec
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.ToolExecute
open Wanxiangshu.Runtime.SubsessionEventRouter

let private todoItemForNativeWrite (item: TodoItem) : obj =
    let statusStr =
        match item.Status with
        | ToolArgs.Todo -> "pending"
        | ToolArgs.InProgress -> "in_progress"
        | ToolArgs.Completed -> "completed"
        | ToolArgs.Cancelled -> "cancelled"

    createObj [ "content", box item.Content; "status", box statusStr ]

let private todoArrayForNativeWrite (decoded: TodoWriteArgs) : obj =
    decoded.Todos |> Array.map todoItemForNativeWrite |> box

let private captureTodoReportFromDecoded
    (host: Host)
    (projection: ProjectionStore)
    (tw: TodoWriteArgs)
    (o: TodoToolOpts)
    : unit =
    if
        o.ToolCallId <> ""
        && (tw.AhaMoments <> ""
            || tw.ChangesAndReasons <> ""
            || tw.Gotchas <> ""
            || tw.LessonsAndConventions <> ""
            || tw.Plan <> "")
    then
        let entry: BacklogEntry =
            { ahaMoments = tw.AhaMoments
              changesAndReasons = tw.ChangesAndReasons
              gotchas = tw.Gotchas
              lessonsAndConventions = tw.LessonsAndConventions
              plan = tw.Plan }

        projection.CaptureBacklogEntry(host, o.ToolCallId, entry)

let private isErrorResult (result: obj) : bool =
    not (Dyn.isNullish result) && not (truthy (Dyn.get result "success"))

let private buildTodoWriteOutput (result: obj) (methodologies: string list) : string =
    if isErrorResult result then
        let errOut = Dyn.str result "output"
        if errOut = "" then Dyn.str result "error" else errOut
    else
        todoWriteOutput methodologies

let private persistTodoWriteSideEffects
    (host: Host)
    (projection: ProjectionStore)
    (tw: TodoWriteArgs)
    (o: TodoToolOpts)
    (opts: obj)
    : JS.Promise<unit> =
    captureTodoReportFromDecoded host projection tw o

    let sid =
        match fromMuxConfig opts with
        | Ok runtime -> workspaceIdString runtime
        | _ -> ""

    if sid = "" then
        Promise.lift ()
    else
        match fromMuxConfig opts with
        | Ok runtime ->
            let root = runtime.Execution.Directory

            if root = "" then
                Promise.lift ()
            else
                promise {
                    do! appendWorkBacklogCommitted root sid tw |> Promise.map ignore

                    let allCompleted =
                        tw.Todos
                        |> Array.forall (fun t ->
                            match t.Status with
                            | Wanxiangshu.Kernel.ToolArgs.TodoItemStatus.Completed
                            | Wanxiangshu.Kernel.ToolArgs.TodoItemStatus.Cancelled -> true
                            | _ -> false)

                    let ev =
                        { CurrentTurnEvidence.empty with
                            Todos = if allCompleted then TodosCompleted else TodosNotCompleted }

                    do! SubsessionEventRouter.routeEvidence root sid ev |> Promise.map ignore
                }
        | _ -> Promise.lift ()

let private wrapTodoWriteResult (result: obj) (output: string) (isError: bool) : obj =
    if Dyn.typeIs result "object" then
        Dyn.withKey result "output" (box output)
    else
        createObj [ "success", box (not isError); "output", box output ]

let private execRunTodoWrite
    (tool: obj)
    (args: obj)
    (opts: obj)
    (host: Host)
    (projection: ProjectionStore)
    : JS.Promise<obj> =
    promise {
        match decodeTodoWriteArgs false args, decodeTodoToolOpts opts with
        | Error e, _
        | _, Error e -> return createObj [ "success", box false; "output", box (wireDecodeFailure "todowrite" e) ]
        | Ok(tw, violations), Ok o ->
            let methodologies = tw.SelectMethodology
            let nativeArgs = createObj [ "todos", todoArrayForNativeWrite tw ]
            let raw = invokeToolExecute tool nativeArgs opts

            let! result =
                if isThenable raw then
                    unbox<JS.Promise<obj>> raw
                else
                    Promise.lift raw

            let output = buildTodoWriteOutput result methodologies
            let isError = isErrorResult result

            if not isError then
                do! persistTodoWriteSideEffects host projection tw o opts

            return wrapTodoWriteResult result output isError
    }

let mkTodoWriteWrapper (host: Host) (projection: ProjectionStore) : obj =
    let wrapperFn =
        System.Func<obj, obj, obj>(fun (tool: obj) (_config: obj) ->
            let execFn =
                System.Func<obj, obj, JS.Promise<obj>>(fun (args: obj) (opts: obj) ->
                    execRunTodoWrite tool args opts host projection)

            createObj
                [ "description", box (toolDescriptionFor host)
                  "parameters", buildWorkBacklogSchema ()
                  "execute", box execFn ])

    createObj [ "targetTool", box (todoWritePromptName host); "wrapper", box wrapperFn ]
