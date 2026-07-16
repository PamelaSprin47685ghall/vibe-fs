module Wanxiangshu.Runtime.Profiler

open Fable.Core
open Fable.Core.JsInterop

let private inspector: obj = importAll "inspector"
let private fs: obj = importAll "fs"

let mutable private activeSession: obj option = None

let start () =
    if activeSession.IsNone then
        let session: obj = emitJsExpr inspector "new $0.Session()"

        session?connect ()
        session?post ("Profiler.enable", (fun () -> session?post ("Profiler.start")))
        session?post ("HeapProfiler.enable", (fun () -> session?post ("HeapProfiler.startSampling")))

        activeSession <- Some session

let private writeFile (path: string) (payload: obj) =
    fs?writeFileSync (path, JS.JSON.stringify payload)

let stopAndSave () =
    match activeSession with
    | Some session ->
        activeSession <- None

        session?post (
            "Profiler.stop",
            fun err (res: obj) ->
                if isNull err then
                    writeFile "/tmp/wanxiangshu.cpuprofile" res?profile

                session?post (
                    "HeapProfiler.stopSampling",
                    fun err2 (res2: obj) ->
                        if isNull err2 then
                            writeFile "/tmp/wanxiangshu.heapprofile" res2?profile

                        session?post ("Profiler.disable")
                        session?post ("HeapProfiler.disable")
                        session?disconnect ()
                )
        )
    | None -> ()

let mutable private initialized = false

let initGlobal () =
    if not initialized then
        initialized <- true

        start ()

        // exit handler cannot run async inspector callbacks; save on a fixed timer instead
        JS.setTimeout stopAndSave (5 * 60 * 1000) |> ignore
