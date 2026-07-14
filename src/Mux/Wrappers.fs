module Wanxiangshu.Mux.Wrappers

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Shell.WorkBacklogSchema
open Wanxiangshu.Shell.TreeSitterShell
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.SessionProjectionStore
open Wanxiangshu.Shell
open Wanxiangshu.Shell.JsonSchemaBuilders
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.DynField
open Wanxiangshu.Shell.MuxHostBindings
open Wanxiangshu.Shell.WorkBacklogToolsCodec
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.ToolExecute
open Wanxiangshu.Shell.ToolContextCodec
open Wanxiangshu.Mux.WrappersReview
open Wanxiangshu.Shell.MuxToolDefinition
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Shell.SubsessionEventRouter

let strField = Wanxiangshu.Shell.DynField.strField
let optInt = Wanxiangshu.Shell.DynField.optInt
let optBool = Wanxiangshu.Shell.DynField.optBool
let optField = Wanxiangshu.Shell.DynField.optField

type JsonSchema = Wanxiangshu.Shell.MuxToolDefinition.JsonSchema
type ToolDefinition = Wanxiangshu.Shell.MuxToolDefinition.ToolDefinition

let requireStrArray (a: obj) (k: string) : string array =
    strListField a k |> Option.map List.toArray |> Option.defaultValue [||]

let mkSchema (props: obj) (required: string array) : JsonSchema =
    MuxToolDefinition.mkSchema props required

let strProp = jsonStrProp
let numProp = jsonNumProp
let boolProp = jsonBoolProp
let strEnumProp = jsonStrEnumProp
let strEnumPropWithDefault = jsonStrEnumPropWithDefault
let strArrayProp = jsonStrArrayProp

let requireWorkspaceId (config: obj) (toolName: string) : Result<string, DomainError> =
    decodeMuxConfig (unbox<IMuxToolContext> config)
    |> Result.map (fun ctx -> ctx.WorkspaceId |> Option.map Id.workspaceIdValue |> Option.defaultValue "")
    |> Result.mapError (function
        | InvalidIntent(_, "workspaceId", _) -> InvalidIntent(toolName, "workspaceId", "required")
        | e -> e)

let private applySyntaxCheck (result: obj) (args: obj) (config: obj) : JS.Promise<obj> =
    promise {
        match extractFilePath args with
        | None -> return result
        | Some filePath ->
            try
                let cwd =
                    match fromMuxConfig config with
                    | Ok runtime -> runtime.Execution.Directory
                    | Error _ -> ""

                let! formatted = readAndCheckSyntax filePath cwd false

                match formatted with
                | None -> return result
                | Some f ->
                    if Dyn.typeIs result "string" then
                        return box (addSyntax (string result) f)
                    elif Dyn.typeIs result "object" && Dyn.truthy (Dyn.get result "success") then
                        let out = Dyn.str result "output"
                        let wrapped = if out <> "" then addSyntax out f else addSyntax "" f
                        return Dyn.withKey result "output" (box wrapped)
                    else
                        return result
            with _ ->
                return result
    }

let private isThenable (value: obj) : bool =
    not (Dyn.isNullish value) && Dyn.typeIs (Dyn.get value "then") "function"

let private mkResultWrapper (targetTool: string) (callback: obj -> obj -> obj -> JS.Promise<obj>) : obj =
    let wrapperFn =
        System.Func<obj, obj, obj>(fun tool config ->
            let orig = tool?execute

            if not (Dyn.typeIs orig "function") then
                tool
            else
                let executeFn =
                    System.Func<obj, obj, JS.Promise<obj>>(fun args opts ->
                        promise {
                            let raw = invokeToolExecute tool args opts

                            let! v =
                                if isThenable raw then
                                    unbox<JS.Promise<obj>> raw
                                else
                                    Promise.lift raw

                            return! callback v args config
                        })

                Dyn.withKey tool "execute" (box executeFn))

    createObj [ "targetTool", box targetTool; "wrapper", box wrapperFn ]

let private mkSyncResultWrapper (targetTool: string) (callback: obj -> obj) : obj =
    mkResultWrapper targetTool (fun result _ _ -> Promise.lift (callback result))

let private mkSyntaxWrappers () : obj array =
    [| mkResultWrapper "file_edit_replace_string" (fun result args config -> applySyntaxCheck result args config)
       mkResultWrapper "file_edit_insert" (fun result args config -> applySyntaxCheck result args config) |]

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

let private mkTodoWriteWrapper (host: Host) (projection: ProjectionStore) : obj =
    let wrapperFn =
        System.Func<obj, obj, obj>(fun (tool: obj) (_config: obj) ->
            let execFn =
                System.Func<obj, obj, JS.Promise<obj>>(fun (args: obj) (opts: obj) ->
                    promise {
                        match decodeTodoWriteArgs false args, decodeTodoToolOpts opts with
                        | Error e, _
                        | _, Error e ->
                            return createObj [ "success", box false; "output", box (wireDecodeFailure "todowrite" e) ]
                        | Ok(tw, violations), Ok o ->
                            let methodologies = tw.SelectMethodology
                            let nativeArgs = createObj [ "todos", todoArrayForNativeWrite tw ]
                            let raw = invokeToolExecute tool nativeArgs opts

                            let! result =
                                if isThenable raw then
                                    unbox<JS.Promise<obj>> raw
                                else
                                    Promise.lift raw

                            let isError = not (Dyn.isNullish result) && not (truthy (Dyn.get result "success"))

                            let output =
                                if isError then
                                    let errOut = Dyn.str result "output"
                                    if errOut = "" then Dyn.str result "error" else errOut
                                else
                                    todoWriteOutput methodologies

                            let sid =
                                match fromMuxConfig opts with
                                | Ok runtime -> workspaceIdString runtime
                                | _ -> ""

                            let toolCallID = o.ToolCallId

                            if not isError then
                                captureTodoReportFromDecoded host projection tw o

                                if sid <> "" then
                                    match fromMuxConfig opts with
                                    | Ok runtime ->
                                        let root = runtime.Execution.Directory

                                        if root <> "" then
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

                                            do!
                                                SubsessionEventRouter.routeToChild
                                                    sid
                                                    (EvidenceUpdated
                                                        { TurnId = TurnId.create ""
                                                          Evidence = ev })
                                                |> Promise.map ignore
                                    | _ -> ()

                            let nextResult =
                                if Dyn.typeIs result "object" then
                                    Dyn.withKey result "output" (box output)
                                else
                                    createObj [ "success", box (not isError); "output", box output ]

                            return nextResult
                    })

            createObj
                [ "description", box (toolDescriptionFor host)
                  "parameters", buildWorkBacklogSchema ()
                  "execute", box execFn ])

    createObj [ "targetTool", box (todoWritePromptName host); "wrapper", box wrapperFn ]

let createAllWrappersFor
    (host: Host)
    (tools: obj)
    (hostReadExec: HostFunctionCapture)
    (scope: RuntimeScope)
    : obj array =
    let projection = scope.Projection

    Array.append
        (mkSyntaxWrappers ())
        [| mkFileReadCapture hostReadExec
           mkTodoWriteWrapper host projection
           mkAgentReportOverride () |]

let createAllWrappers (tools: obj) (hostReadExec: HostFunctionCapture) (scope: RuntimeScope) : obj array =
    createAllWrappersFor mux tools hostReadExec scope
