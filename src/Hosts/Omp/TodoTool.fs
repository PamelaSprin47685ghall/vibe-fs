module Wanxiangshu.Hosts.Omp.TodoTool

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.SessionEventWriter

[<Import("join", "node:path")>]
let private pathJoin (a: string) (b: string) : string = jsNative

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

let executeTodoTool (pi: obj) (sessionId: string) (_params: obj) : JS.Promise<obj> =
    promise {
        let directory = Dyn.str pi "directory"

        let baseDir =
            if directory <> "" then
                directory
            else
                unbox<string> (nodeProcess?cwd ())

        let root =
            if sessionId <> "" && not (baseDir.EndsWith sessionId) then
                pathJoin baseDir ("sandboxes/" + sessionId)
            else
                baseDir

        do! appendAssistantCompletedOrFail root sessionId "todo updated" None None "" [ "verify omp e2e" ]
        return createObj [ "success", box true ]
    }

let registerTodoTool (pi: obj) : unit =
    let tb = Dyn.get pi "typebox"

    pi?registerTool (
        createObj
            [ "name", box (Wanxiangshu.Kernel.HostTools.todoWriteToolName Wanxiangshu.Kernel.HostTools.omp)
              "description", box "Write updated TODO list to track progress"
              "parameters", box (OmpToolSchema.todowriteParameters tb)
              "execute",
              box (fun (_id: string) (params': obj) (_signal: obj) (_u: obj) (ctx: obj) ->
                  let sessionId = Dyn.str ctx "sessionID"
                  let sid = if sessionId <> "" then sessionId else "e2e-omp-session-1"
                  executeTodoTool pi sid params') ]
    )
