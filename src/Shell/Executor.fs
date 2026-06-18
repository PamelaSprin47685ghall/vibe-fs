module VibeFs.Shell.Executor

open Fable.Core
open Fable.Core.JsInterop
open System.Text.RegularExpressions
open VibeFs.Kernel
open VibeFs.Kernel.Executor
open VibeFs.Shell.ExecutorJavascript

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

let isWindows () : bool = platform = "win32"

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

let executorLogDir : string = join (tmpdir ()) "omp-kunwei-executor"

let private sanitizeId (id: string) : string = Regex.Replace(id, "/", "-")

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

type RunOutcome = { stdout: string; stderr: string; code: int option; timedOut: bool }

let private awaitChild (child: SpawnedChild) (kill: SpawnedChild -> unit) (timeoutMs: int option) : JS.Promise<RunOutcome> =
    let work =
        Async.FromContinuations(fun (resolve, reject, _) ->
            let stdout = ResizeArray<string>()
            let stderr = ResizeArray<string>()
            let mutable settled = false
            let removeListeners () =
                try child?stdout?removeAllListeners() |> ignore with _ -> ()
                try child?stderr?removeAllListeners() |> ignore with _ -> ()
            let settle code timedOut =
                if not settled then
                    settled <- true
                    removeListeners ()
                    resolve { stdout = String.concat "" stdout
                              stderr = String.concat "" stderr
                              code = code
                              timedOut = timedOut }
            child?stdout?on("data", fun (c: obj) -> stdout.Add(string c)) |> ignore
            child?stderr?on("data", fun (c: obj) -> stderr.Add(string c)) |> ignore
            child?on("error", fun (e: obj) -> reject (e :?> exn)) |> ignore
            child?on("close", fun (code: obj) ->
                settle (if isNull code then None else Some(unbox<int> code)) false) |> ignore
            match timeoutMs with
            | None -> ()
            | Some ms ->
                async {
                    do! Async.Sleep ms
                    if not settled then
                        kill child
                        settle None true
                }
                |> Async.StartImmediate
        )
    work |> Async.StartAsPromise

let spawnAndRun (command: string) (args: string array) (cwd: string) (timeoutMs: int option)
                : JS.Promise<RunOutcome> =
    let child = spawnChild command args cwd
    awaitChild child killTree timeoutMs

let runScript (interpreter: string) (interpreterArgs: string array) (cwd: string)
              (scriptPath: string) (timeoutMs: int option) : JS.Promise<RunOutcome> =
    spawnAndRun interpreter (Array.append interpreterArgs [| scriptPath |]) cwd timeoutMs

let private runShellProgram (program: string) (cwd: string) (sessionId: string) (timeoutMs: int) : JS.Promise<RunOutcome> =
    let extension = if isWindows () then "ps1" else "sh"
    let scriptPath = createTempScript (getExecutorTempScriptPath sessionId extension) program
    if isWindows ()
    then runScript "powershell.exe" [| "-ExecutionPolicy"; "Bypass"; "-File" |] cwd scriptPath (Some timeoutMs)
    else runScript "bash" [||] cwd scriptPath (Some timeoutMs)

let private runPythonProgram (program: string) (dependencies: string list) (cwd: string)
                             (sessionId: string) (timeoutMs: int) : JS.Promise<RunOutcome> =
    let scriptPath = createTempScript (getExecutorTempScriptPath sessionId "py") program
    let baseArgs = ResizeArray([ "--isolated" ])
    dependencies |> List.iter (fun dep -> baseArgs.Add "--with"; baseArgs.Add dep)
    let warmup () =
        spawnAndRun "uvx" (Array.append (baseArgs.ToArray()) [| "--from"; "python"; "python"; "-c"; "pass" |]) cwd None
    async {
        if not dependencies.IsEmpty then
            let! warm = warmup () |> Async.AwaitPromise
            if warm.code <> Some 0 then return warm
            else
                return! runScript "uvx" (Array.append (baseArgs.ToArray()) [| "--from"; "python"; "python" |]) cwd scriptPath (Some timeoutMs)
                          |> Async.AwaitPromise
        else
            return! runScript "uvx" (Array.append (baseArgs.ToArray()) [| "--from"; "python"; "python" |]) cwd scriptPath (Some timeoutMs)
                      |> Async.AwaitPromise
    }
    |> Async.StartAsPromise

let private runJavascriptProgram (program: string) (dependencies: string list) (cwd: string)
                                 (sessionId: string) (timeoutMs: int) : JS.Promise<RunOutcome> =
    async {
        let projectDir = getExecutorProjectDir (Some sessionId)
        do! ensureJavascriptProject projectDir dependencies |> Async.AwaitPromise
        let! rewritten = rewriteJavascriptModuleSpecifiers program cwd |> Async.AwaitPromise
        let body = $"{createJavascriptPrelude cwd}{rewritten}"
        let scriptPath = createTempScript $"{projectDir}/script.mts" body
        return! runScript "npx" [| "--prefix"; projectDir; "--yes"; "--no-install"; "tsx" |] cwd scriptPath (Some timeoutMs)
                  |> Async.AwaitPromise
    }
    |> Async.StartAsPromise

let private isErrnoException (error: obj) : bool =
    not (isNull error) && string error?("code") = "ENOENT"

let missingExecutableFor (language: ExecutorLanguage) : string =
    match language with
    | Python -> "uvx"
    | Javascript -> "npx"
    | Shell -> if isWindows () then "powershell.exe" else "bash"

let mapOutcome (options: ExecuteOptions) (timeout: int) (output: string) (outcome: RunOutcome)
               : ExecuteResult =
    if outcome.timedOut then
        let partial = if output = "" then "(no output before timeout)" else output
        let suffix = $"\n[executor] Killed after {timeout}ms ({options.timeoutType}). Partial output returned."
        Truncated(output = (partial + suffix).Trim(), timeoutType = options.timeoutType)
    else
        match outcome.code with
        | Some 0 -> Completed(output = if output = "" then "(no output)" else output)
        | Some code -> Failed(output = if output = "" then $"exited with code {code}" else output)
        | None -> Failed(output = if output = "" then "exited with no code" else output)

type ExecuteDeps = {
    runProgram: string -> ExecutorLanguage -> string list -> string -> string -> int -> JS.Promise<RunOutcome>
}

let private defaultRunProgram (program: string) (language: ExecutorLanguage) (dependencies: string list)
                              (cwd: string) (sessionId: string) (timeout: int) : JS.Promise<RunOutcome> =
    match language with
    | Shell -> runShellProgram program cwd sessionId timeout
    | Python -> runPythonProgram program dependencies cwd sessionId timeout
    | Javascript -> runJavascriptProgram program dependencies cwd sessionId timeout

let defaultExecuteDeps : ExecuteDeps = { runProgram = defaultRunProgram }

let executeWith (deps: ExecuteDeps) (options: ExecuteOptions) (sessionId: string)
                : JS.Promise<ExecuteResult> =
    async {
        let timeout = timeoutMs options.timeoutType
        let cwd = defaultArg options.cwd (nodeProcess?cwd())
        let program = if options.language = Shell then prepareShellProgram options.program else options.program
        try
            let! outcome =
                deps.runProgram program options.language options.dependencies cwd sessionId timeout
                |> Async.AwaitPromise
            let output = (outcome.stdout + outcome.stderr).Trim()
            return mapOutcome options timeout output outcome
        with error ->
            if isErrnoException error then
                let executable = missingExecutableFor options.language
                return MissingExecutable(executable = executable,
                                         output = $"Error: '{executable}' executable not found. Please ensure '{executable}' is installed and available on your PATH.")
            else return raise error
    }
    |> Async.StartAsPromise

let execute (options: ExecuteOptions) (sessionId: string) : JS.Promise<ExecuteResult> =
    executeWith defaultExecuteDeps options sessionId
