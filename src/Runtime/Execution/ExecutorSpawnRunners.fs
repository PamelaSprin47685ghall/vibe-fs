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
open Wanxiangshu.Runtime.ExecutorSpawnHelper

[<Emit("performance.now()")>]
let private now () : float = jsNative

let private runShellProgram scope program cwd sessionId deadline onKillRegistered : JS.Promise<RunOutcome> =
    let remaining = ExecutionDeadline.remainingMs now deadline

    if remaining <= 0 then
        Promise.lift (TimedOut("", "(execution deadline exceeded before start)"))
    else
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
                (Some remaining)
                sid
                onKillRegistered
        else
            runScript scope "bash" [||] cwd scriptPath (Some remaining) sid onKillRegistered

let private runPythonWithDependencies
    scope
    baseArgs
    cwd
    scriptPath
    sid
    deadline
    remaining
    onKillRegistered
    : JS.Promise<RunOutcome> =
    promise {
        let! warm =
            spawnAndRun
                scope
                "uvx"
                (Array.append baseArgs [| "--from"; "python"; "python"; "-c"; "pass" |])
                cwd
                (Some remaining)
                sid
                None

        match warm with
        | Exited(0, _, _) ->
            let scriptRemaining = ExecutionDeadline.remainingMs now deadline

            if scriptRemaining <= 0 then
                return TimedOut("", "(execution deadline exceeded after python warmup)")
            else
                return!
                    runScript
                        scope
                        "uvx"
                        (Array.append baseArgs [| "--from"; "python"; "python" |])
                        cwd
                        scriptPath
                        (Some scriptRemaining)
                        sid
                        onKillRegistered
        | _ -> return warm
    }

let private runPythonProgram
    scope
    program
    (dependencies: string list)
    cwd
    sessionId
    deadline
    onKillRegistered
    : JS.Promise<RunOutcome> =
    let remaining = ExecutionDeadline.remainingMs now deadline

    if remaining <= 0 then
        Promise.lift (TimedOut("", "(execution deadline exceeded before start)"))
    else
        let scriptPath = createTempScript (getExecutorTempScriptPath sessionId "py") program

        let baseArgs =
            [| yield "--isolated"
               for dep in dependencies do
                   yield "--with"
                   yield dep |]

        let sid = Some sessionId

        if not dependencies.IsEmpty then
            runPythonWithDependencies scope baseArgs cwd scriptPath sid deadline remaining onKillRegistered
        else
            runScript
                scope
                "uvx"
                (Array.append baseArgs [| "--from"; "python"; "python" |])
                cwd
                scriptPath
                (Some remaining)
                sid
                onKillRegistered

let private runJavascriptProgram
    scope
    program
    dependencies
    cwd
    sessionId
    deadline
    onKillRegistered
    : JS.Promise<RunOutcome> =
    promise {
        let remaining = ExecutionDeadline.remainingMs now deadline

        if remaining <= 0 then
            return TimedOut("", "(execution deadline exceeded before start)")
        else
            let projectDir = getExecutorProjectDir (Some sessionId)
            let! installRes = ensureJavascriptProject scope projectDir dependencies (Some remaining) (Some sessionId)

            match installRes with
            | Some(Exited(0, _, _))
            | None ->
                let scriptRemaining = ExecutionDeadline.remainingMs now deadline

                if scriptRemaining <= 0 then
                    return TimedOut("", "(execution deadline exceeded after javascript dependency setup)")
                else
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
                            (Some scriptRemaining)
                            (Some sessionId)
                            onKillRegistered
            | Some otherOutcome -> return otherOutcome
    }

type ExecuteDeps =
    { runProgram:
        RuntimeScope
            -> string
            -> ExecutorLanguage
            -> string list
            -> string
            -> string
            -> ExecutionDeadline
            -> ((unit -> unit) -> unit) option
            -> JS.Promise<RunOutcome> }

let defaultRunProgram
    scope
    program
    language
    dependencies
    cwd
    sessionId
    deadline
    onKillRegistered
    : JS.Promise<RunOutcome> =
    match language with
    | Shell -> runShellProgram scope program cwd sessionId deadline onKillRegistered
    | Python -> runPythonProgram scope program dependencies cwd sessionId deadline onKillRegistered
    | Javascript -> runJavascriptProgram scope program dependencies cwd sessionId deadline onKillRegistered

let defaultExecuteDeps: ExecuteDeps = { runProgram = defaultRunProgram }
