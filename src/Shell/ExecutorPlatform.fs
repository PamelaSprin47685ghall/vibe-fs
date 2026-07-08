module Wanxiangshu.Shell.ExecutorPlatform

open Fable.Core
open Fable.Core.JsInterop
open System.Text.RegularExpressions
open Wanxiangshu.Kernel.Domain

[<Global("process")>]
let private nodeProcess: obj = jsNative

let platform: string = nodeProcess?platform

let isWindows () : bool = platform = "win32"

let private processKill (pid: int) (signal: string) : unit =
    nodeProcess?kill (pid, signal) |> ignore

let env () : obj =
    let copy = createObj []
    let copyTarget: obj = nodeProcess?env
    // 显式赋值，代替 Dyn.assignInto 以便与 Kernel 隔离
    let keys = Fable.Core.JS.Constructors.Object.keys (copyTarget)

    for k in keys do
        copy?(k) <- copyTarget?(k)

    copy

[<Import("spawn", "node:child_process")>]
let private spawn (cmd: string) (args: string array) (opts: obj) : obj = jsNative

[<Import("writeFileSync", "node:fs")>]
let private writeFileSync (path: string) (content: string) (encoding: string) : unit = jsNative

[<Import("chmodSync", "node:fs")>]
let private chmodSync (path: string) (mode: int) : unit = jsNative

[<Import("tmpdir", "node:os")>]
let private tmpdir () : string = jsNative

[<Import("join", "node:path")>]
let private join (a: string) (b: string) : string = jsNative

[<Import("mkdirSync", "node:fs")>]
let private mkdirSync (dir: string) (opts: obj) : unit = jsNative

let executorLogDir: string = join (tmpdir ()) "omp-wanxiangshu-executor"

let private sanitizeId (id: string) : string = Regex.Replace(id, "/", "-")

type SpawnedChild =
    abstract member stdout: obj with get
    abstract member stderr: obj with get
    abstract member on: event: string * handler: obj -> unit
    abstract member kill: signal: string -> bool

let killTree (childProcess: obj) : unit =
    let pid = childProcess?("pid")

    if isNull pid then
        ()
    else
        let pidNum = unbox<int> pid

        try
            if platform = "win32" then
                spawn "taskkill" [| "/F"; "/T"; "/PID"; string pidNum |] (box {| stdio = "ignore" |})
                |> ignore
            else
                processKill -pidNum "SIGKILL"
        with _ ->
            try
                ((childProcess :?> SpawnedChild).kill "SIGKILL") |> ignore
            with _ ->
                ()

let spawnChild (command: string) (args: string array) (cwd: string) : SpawnedChild =
    let opts =
        box
            {| cwd = cwd
               env = env ()
               stdio = [| "ignore"; "pipe"; "pipe" |]
               detached = platform <> "win32"
               windowsHide = true |}

    spawn command args opts :?> SpawnedChild

let createTempScript (scriptPath: string) (program: string) : string =
    writeFileSync scriptPath program "utf-8"

    if platform <> "win32" then
        chmodSync scriptPath 0o755

    scriptPath

let private ensureDir (dir: string) : unit =
    mkdirSync dir (box {| recursive = true |})

let getExecutorProjectDir (sessionId: string option) : string =
    let dir =
        match sessionId with
        | None -> join executorLogDir "executor"
        | Some sid -> join executorLogDir $"executor-{sanitizeId sid}"

    ensureDir dir
    dir

let getExecutorTempScriptPath (sessionId: string) (extension: string) : string =
    $"{getExecutorProjectDir (Some sessionId)}/script.{extension}"
