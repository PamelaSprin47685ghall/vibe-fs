module Wanxiangshu.Runtime.ExecutorSpawnRunners

open Fable.Core
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Runtime.ExecutorFormat
open Wanxiangshu.Runtime.ExecutorJavascript
open Wanxiangshu.Runtime.ExecutorSpawn
open Wanxiangshu.Runtime.ExecutorPlatform
open Wanxiangshu.Runtime.RuntimeScope

let private runShellProgram
    (scope: RuntimeScope)
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
            scope
            "powershell.exe"
            [| "-ExecutionPolicy"; "Bypass"; "-File" |]
            cwd
            scriptPath
            (Some timeoutMs)
            sid
            onKillRegistered
    else
        runScript scope "bash" [||] cwd scriptPath (Some timeoutMs) sid onKillRegistered

let private runPythonProgram
    (scope: RuntimeScope)
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
        spawnAndRun
            scope
            "uvx"
            (Array.append baseArgs [| "--from"; "python"; "python"; "-c"; "pass" |])
            cwd
            None
            None
            None

    promise {
        if not dependencies.IsEmpty then
            let! warm = warmup ()

            match warm with
            | Exited(0, _, _) ->
                return!
                    runScript
                        scope
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
                    scope
                    "uvx"
                    (Array.append baseArgs [| "--from"; "python"; "python" |])
                    cwd
                    scriptPath
                    (Some timeoutMs)
                    sid
                    onKillRegistered
    }

let private runJavascriptProgram
    (scope: RuntimeScope)
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
                scope
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
        RuntimeScope
            -> string
            -> ExecutorLanguage
            -> string list
            -> string
            -> string
            -> int
            -> ((unit -> unit) -> unit) option
            -> JS.Promise<RunOutcome> }

let defaultRunProgram
    (scope: RuntimeScope)
    (program: string)
    (language: ExecutorLanguage)
    (dependencies: string list)
    (cwd: string)
    (sessionId: string)
    (timeout: int)
    (onKillRegistered: ((unit -> unit) -> unit) option)
    : JS.Promise<RunOutcome> =
    match language with
    | Shell -> runShellProgram scope program cwd sessionId timeout onKillRegistered
    | Python -> runPythonProgram scope program dependencies cwd sessionId timeout onKillRegistered
    | Javascript -> runJavascriptProgram scope program dependencies cwd sessionId timeout onKillRegistered

let defaultExecuteDeps: ExecuteDeps = { runProgram = defaultRunProgram }
