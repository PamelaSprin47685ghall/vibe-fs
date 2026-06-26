module Wanxiangshu.Shell.ExecutorSpawn

open Fable.Core
open Fable.Core.JsInterop
open System.Text.RegularExpressions
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Shell.ExecutorJavascript
open Wanxiangshu.Shell.SessionExecutor
module Dyn = Wanxiangshu.Shell.Dyn

[<Global("process")>]
let private nodeProcess : obj = jsNative

let private platform : string = nodeProcess?platform

let private processKill (pid: int) (signal: string) : unit = nodeProcess?kill(pid, signal) |> ignore

let private env () : obj =
    let copy = createObj []
    Dyn.assignInto copy nodeProcess?env |> ignore
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

let executorLogDir : string = join (tmpdir ()) "omp-wanxiangshu-executor"

let private sanitizeId (id: string) : string = Regex.Replace(id, "/", "-")

let isWindows () : bool = platform = "win32"

type SpawnedChild =
    abstract member stdout: obj with get
    abstract member stderr: obj with get
    abstract member on: event: string * handler: obj -> unit
    abstract member kill: signal: string -> bool

let killTree (childProcess: obj) : unit =
    let pid = childProcess?("pid")
    if isNull pid then ()
    else
        let pidNum = unbox<int> pid
        try
            if platform = "win32" then
                spawn "taskkill" [| "/F"; "/T"; "/PID"; string pidNum |] (box {| stdio = "ignore" |}) |> ignore
            else
                processKill pidNum "SIGKILL"
        with _ ->
            try ((childProcess :?> SpawnedChild).kill "SIGKILL") |> ignore with _ -> ()

let spawnChild (command: string) (args: string array) (cwd: string) : SpawnedChild =
    let opts =
        box {| cwd = cwd
               env = env ()
               stdio = [| "ignore"; "pipe"; "pipe" |]
               detached = platform <> "win32"
               windowsHide = true |}
    spawn command args opts :?> SpawnedChild

let createTempScript (scriptPath: string) (program: string) : string =
    writeFileSync scriptPath program "utf-8"
    if platform <> "win32" then chmodSync scriptPath 0o755
    scriptPath

let getExecutorProjectDir (sessionId: string option) : string =
    match sessionId with
    | None ->
        let dir = join executorLogDir "executor"
        mkdirSync dir (box {| recursive = true |})
        dir
    | Some sid ->
        let dir = join executorLogDir $"executor-{sanitizeId sid}"
        mkdirSync dir (box {| recursive = true |})
        dir

let getExecutorTempScriptPath (sessionId: string) (extension: string) : string =
    $"{getExecutorProjectDir (Some sessionId)}/script.{extension}"

type RunOutcome =
    | Exited of code: int * stdout: string * stderr: string
    | TimedOut of stdout: string * stderr: string
    | Signaled of signal: string * stdout: string * stderr: string
    | SpawnFailed of reason: DomainError

let private awaitChild (child: SpawnedChild) (executable: string) (kill: SpawnedChild -> unit)
                       (timeoutMs: int option) (sessionId: string option)
                       (onKillRegistered: ((unit -> unit) -> unit) option) : JS.Promise<RunOutcome> =
    Promise.create (fun resolve _reject ->
        let stdout = ResizeArray<string>()
        let stderr = ResizeArray<string>()
        let mutable settled = false
        let doKill () = kill child
        let removeListeners () =
            try child?stdout?removeAllListeners() |> ignore with _ -> ()
            try child?stderr?removeAllListeners() |> ignore with _ -> ()
        let settle outcome =
            if not settled then
                settled <- true
                removeListeners ()
                unregisterActiveRun (defaultArg sessionId "")
                resolve outcome
        match sessionId with
        | Some sid when sid <> "" ->
            registerActiveRun sid doKill
            onKillRegistered |> Option.iter (fun register -> register doKill)
        | _ -> ()
        child?stdout?on("data", fun (c: obj) -> stdout.Add(string c)) |> ignore
        child?stderr?on("data", fun (c: obj) -> stderr.Add(string c)) |> ignore
        child?on("error", fun (_e: obj) -> settle (SpawnFailed(ExecutorExecutableMissing executable))) |> ignore
        child?on("close", fun (code: obj) (signal: obj) ->
            let capturedOut = String.concat "" stdout
            let capturedErr = String.concat "" stderr
            settle (
                if isNull code then
                    let sigName = if isNull signal then "unknown" else string signal
                    Signaled(sigName, capturedOut, capturedErr)
                else Exited(unbox<int> code, capturedOut, capturedErr))) |> ignore
        let onTimeout () =
            if not settled then (doKill (); settle (TimedOut(String.concat "" stdout, String.concat "" stderr)))
        match timeoutMs with
        | None -> ()
        | Some ms ->
            promise {
                do! Promise.sleep ms
                onTimeout ()
            }
            |> Promise.start)

let spawnAndRun (command: string) (args: string array) (cwd: string) (timeoutMs: int option)
                (sessionId: string option) (onKillRegistered: ((unit -> unit) -> unit) option)
                : JS.Promise<RunOutcome> =
    let child = spawnChild command args cwd
    awaitChild child command killTree timeoutMs sessionId onKillRegistered

let runScript (interpreter: string) (interpreterArgs: string array) (cwd: string)
              (scriptPath: string) (timeoutMs: int option) (sessionId: string option)
              (onKillRegistered: ((unit -> unit) -> unit) option) : JS.Promise<RunOutcome> =
    spawnAndRun interpreter (Array.append interpreterArgs [| scriptPath |]) cwd timeoutMs sessionId onKillRegistered

let missingExecutableFor (language: ExecutorLanguage) : string =
    match language with
    | Python -> "uvx"
    | Javascript -> "npx"
    | Shell -> if isWindows () then "powershell.exe" else "bash"

let partialStdout (output: string) =
    if output = "" then "(no output before termination)" else output
