module Wanxiangshu.Runtime.Profiler

open Fable.Core
open Fable.Core.JsInterop

let private inspector: obj = importAll "inspector"
let private fs: obj = importAll "fs"
let private nodeProcess: obj = importAll "node:process"

let private activeSession = ref<Option<obj>> None

let private resolveOutputDir (fallback: string option) : string =
    let envDir =
        try
            unbox<string> (nodeProcess?env?WANXIANGSHU_PROFILER_DIR)
        with _ ->
            null

    match fallback, envDir with
    | Some d, _ -> d
    | _, d when not (isNull d) && d.Length > 0 -> d
    | _ -> "/tmp"

let private uniqueToken () =
    let pid: int = unbox<int> (nodeProcess?pid)
    let ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() |> int
    let rand = JS.Math.random () * 1e6 |> int
    $"{pid}-{ts}-{rand}"

let private writeFile (path: string) (payload: obj) =
    fs?writeFileSync (path, JS.JSON.stringify payload)

let start () =
    if Option.isNone activeSession.Value then
        let session: obj = emitJsExpr inspector "new $0.Session()"

        session?connect ()
        session?post ("Profiler.enable", (fun () -> session?post ("Profiler.start")))

        try
            session?post (
                "HeapProfiler.enable",
                (fun () ->
                    try
                        session?post ("HeapProfiler.startSampling")
                    with _ ->
                        ())
            )
        with _ ->
            ()

        activeSession.Value <- Some session

let stopAndSave (outputDir: string option) =
    match activeSession.Value with
    | Some session ->
        activeSession.Value <- None
        let dir = resolveOutputDir outputDir
        let token = uniqueToken ()

        session?post (
            "Profiler.stop",
            fun err (res: obj) ->
                if isNull err then
                    writeFile $"{dir}/{token}.cpu.profile" res?profile

                session?post (
                    "HeapProfiler.stopSampling",
                    fun err2 (res2: obj) ->
                        if isNull err2 then
                            writeFile $"{dir}/{token}.heap.profile" res2?profile

                        session?post ("Profiler.disable")
                        session?post ("HeapProfiler.disable")
                        session?disconnect ()
                )
        )
    | None -> ()
