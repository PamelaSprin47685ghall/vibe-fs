module Wanxiangshu.Shell.Profiler

open Fable.Core
open Fable.Core.JsInterop

let private inspector: obj = importAll "inspector"
let private fs: obj = importAll "fs"

let mutable private activeSession: obj option = None

let start () =
    if activeSession.IsNone then
        let session: obj = emitJsExpr inspector "new $0.Session()"
        
        session?connect()
        session?post("Profiler.enable", fun () ->
            session?post("Profiler.start")
        )
        
        activeSession <- Some session

let stopAndSave () =
    match activeSession with
    | Some session ->
        let cb = fun err (res: obj) ->
            if not (isNull err) then
                () // error silently ignored
            else
                let targetPath = "/tmp/wanxiangshu.cpuprofile"
                fs?writeFileSync(targetPath, JS.JSON.stringify(res?profile))
            
            session?post("Profiler.disable")
            session?disconnect()
            activeSession <- None
            
        session?post("Profiler.stop", cb)
    | None -> ()

let mutable private initialized = false

let initGlobal () =
    if not initialized then
        initialized <- true
        let processObj: obj = importDefault "process"
        
        // Auto start
        start()
        
        // Auto stop on exit
        processObj?on("exit", fun () -> 
            stopAndSave()
        ) |> ignore
