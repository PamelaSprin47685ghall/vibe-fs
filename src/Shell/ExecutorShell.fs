module VibeFs.Shell.ExecutorShell

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.ExecutorKernel
open VibeFs.Kernel.HeadTail
open VibeFs.Shell.ExecutorProcess
open VibeFs.Shell.ExecutorScript
open VibeFs.Shell.ExecutorPaths
open VibeFs.Shell.ExecutorJavascript

[<Global("process")>]
let private nodeProcess : obj = jsNative

type RunOutcome = { stdout: string; stderr: string; code: int option; timedOut: bool }

/// Await a spawned child's completion, collecting stdout/stderr and enforcing a
/// timeout by killing the tree.
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
                |> Async.Start
        )
    work |> Async.StartAsPromise


/// Spawn a command and await it with a timeout, killing the tree on expiry.
let spawnAndRun (command: string) (args: string array) (cwd: string) (timeoutMs: int option)
                : JS.Promise<RunOutcome> =
    let child = spawnChild command args cwd
    awaitChild child killTree timeoutMs

/// Write a program to a temp script and run it with the given interpreter.
let runScript (interpreter: string) (interpreterArgs: string array) (cwd: string)
              (scriptPath: string) (timeoutMs: int option) : JS.Promise<RunOutcome> =
    spawnAndRun interpreter (Array.append interpreterArgs [| scriptPath |]) cwd timeoutMs

let private runShellProgram (program: string) (cwd: string) (sessionId: string) (timeoutMs: int) : JS.Promise<RunOutcome> =
    let extension = if isWindows () then "ps1" else "sh"
    let scriptPath = createTempScript (getTempScriptPath sessionId extension) program
    if isWindows ()
    then runScript "powershell.exe" [| "-ExecutionPolicy"; "Bypass"; "-File" |] cwd scriptPath (Some timeoutMs)
    else runScript "bash" [||] cwd scriptPath (Some timeoutMs)

let private runPythonProgram (program: string) (dependencies: string list) (cwd: string)
                             (sessionId: string) (timeoutMs: int) : JS.Promise<RunOutcome> =
    let scriptPath = createTempScript (getTempScriptPath sessionId "py") program
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

/// The executable name reported when a spawn fails with ENOENT.
let missingExecutableFor (language: ExecutorLanguage) : string =
    match language with
    | Python -> "uvx"
    | Javascript -> "npx"
    | Shell -> if isWindows () then "powershell.exe" else "bash"

/// Pure mapping from a raw run outcome to the typed ExecuteResult.  Extracted so
/// the result-shaping logic is directly testable without spawning processes.
let mapOutcome (options: ExecuteOptions) (timeout: int) (output: string) (outcome: RunOutcome)
               : ExecuteResult =
    if outcome.timedOut then
        let partial = if output = "" then "(no output before timeout)" else output
        let suffix = $"\n[executor] Timed out after {timeout}ms ({options.timeoutType}). Partial output returned."
        Truncated(output = (partial + suffix).Trim(), timeoutType = options.timeoutType)
    else
        match outcome.code with
        | Some 0 -> Completed(output = if output = "" then "(no output)" else output)
        | Some code -> Failed(output = if output = "" then $"exited with code {code}" else output)
        | None -> Failed(output = if output = "" then "exited with no code" else output)

/// Host-injectable program runner — mirrors the original `deps.runProgram`
/// so tests and hosts can substitute execution.
type ExecuteDeps = {
    runProgram: string -> ExecutorLanguage -> string list -> string -> string -> int -> JS.Promise<RunOutcome>
}

/// Default deps dispatch to the language-specific runners.
let private defaultRunProgram (program: string) (language: ExecutorLanguage) (dependencies: string list)
                              (cwd: string) (sessionId: string) (timeout: int) : JS.Promise<RunOutcome> =
    match language with
    | Shell -> runShellProgram program cwd sessionId timeout
    | Python -> runPythonProgram program dependencies cwd sessionId timeout
    | Javascript -> runJavascriptProgram program dependencies cwd sessionId timeout

let defaultExecuteDeps : ExecuteDeps = { runProgram = defaultRunProgram }

/// Run a program, mapping the raw outcome into the typed ExecuteResult DU.
/// `executeWith` accepts injectable deps for testing; `execute` uses the defaults.
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
            let combined = (outcome.stdout + outcome.stderr).Trim()
            let output = formatSafetyWarning combined options.program options.language
            return mapOutcome options timeout output outcome
        with error ->
            if isErrnoException error then
                let executable = missingExecutableFor options.language
                return MissingExecutable(executable = executable,
                                         output = $"Error: '{executable}' executable not found. Please ensure '{executable}' is installed and available on your PATH.")
            else return raise error
    }
    |> Async.StartAsPromise

/// Run a program with the default (real) runners.
let execute (options: ExecuteOptions) (sessionId: string) : JS.Promise<ExecuteResult> =
    executeWith defaultExecuteDeps options sessionId
