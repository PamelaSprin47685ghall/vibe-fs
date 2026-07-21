module Wanxiangshu.Shell.ExecutorSpawnRunners

open Fable.Core
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Shell.ExecutorJavascript
open Wanxiangshu.Shell.ExecutorSpawn
open Wanxiangshu.Shell.ExecutorPlatform

let private runShellProgram
    (program: string)
    (cwd: string)
    (sessionId: string)
    (timeoutMs: int)
    (onKillRegistered: ((unit -> unit) -> unit) option)
    : JS.Promise<RunOutcome> =
    let extension = if isWindows () then "ps1" else "sh"

    let scriptPath =
        createTempScript (getExecutorTempScriptPath sessionId extension) program

    let sid = Some sessionId

    if isWindows () then
        runScript
            "powershell.exe"
            [| "-ExecutionPolicy"; "Bypass"; "-File" |]
            cwd
            scriptPath
            (Some timeoutMs)
            sid
            onKillRegistered
    else
        runScript "bash" [||] cwd scriptPath (Some timeoutMs) sid onKillRegistered

let private runPythonProgram
    (program: string)
    (dependencies: string list)
    (cwd: string)
    (sessionId: string)
    (timeoutMs: int)
    (onKillRegistered: ((unit -> unit) -> unit) option)
    : JS.Promise<RunOutcome> =
    let scriptPath = createTempScript (getExecutorTempScriptPath sessionId "py") program

    let baseArgs =
        [| yield "--isolated"
           for dep in dependencies do
               yield "--with"
               yield dep |]

    let sid = Some sessionId

    let warmup () =
        spawnAndRun "uvx" (Array.append baseArgs [| "--from"; "python"; "python"; "-c"; "pass" |]) cwd (Some 60000) sid None

    promise {
        if not dependencies.IsEmpty then
            let! warm = warmup ()

            match warm with
            | Exited(0, _, _) ->
                return!
                    runScript
                        "uvx"
                        (Array.append baseArgs [| "--from"; "python"; "python" |])
                        cwd
                        scriptPath
                        (Some timeoutMs)
                        sid
                        onKillRegistered
            | _ -> return warm
        else
            return!
                runScript
                    "uvx"
                    (Array.append baseArgs [| "--from"; "python"; "python" |])
                    cwd
                    scriptPath
                    (Some timeoutMs)
                    sid
                    onKillRegistered
    }

let private runJavascriptProgram
    (program: string)
    (dependencies: string list)
    (cwd: string)
    (sessionId: string)
    (timeoutMs: int)
    (onKillRegistered: ((unit -> unit) -> unit) option)
    : JS.Promise<RunOutcome> =
    promise {
        let projectDir = getExecutorProjectDir (Some sessionId)
        do! ensureJavascriptProject projectDir dependencies
        let! rewritten = rewriteJavascriptModuleSpecifiers program cwd
        let body = $"{createJavascriptPrelude cwd}{rewritten}"
        let scriptPath = createTempScript $"{projectDir}/script.mts" body

        return!
            runScript
                "npx"
                [| "--prefix"; projectDir; "--yes"; "--no-install"; "tsx" |]
                cwd
                scriptPath
                (Some timeoutMs)
                (Some sessionId)
                onKillRegistered
    }

type ExecuteDeps =
    { runProgram:
        string
            -> ExecutorLanguage
            -> string list
            -> string
            -> string
            -> int
            -> ((unit -> unit) -> unit) option
            -> JS.Promise<RunOutcome> }

let defaultRunProgram
    (program: string)
    (language: ExecutorLanguage)
    (dependencies: string list)
    (cwd: string)
    (sessionId: string)
    (timeout: int)
    (onKillRegistered: ((unit -> unit) -> unit) option)
    : JS.Promise<RunOutcome> =
    match language with
    | Shell -> runShellProgram program cwd sessionId timeout onKillRegistered
    | Python -> runPythonProgram program dependencies cwd sessionId timeout onKillRegistered
    | Javascript -> runJavascriptProgram program dependencies cwd sessionId timeout onKillRegistered

let defaultExecuteDeps: ExecuteDeps = { runProgram = defaultRunProgram }
