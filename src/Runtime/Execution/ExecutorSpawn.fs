module Wanxiangshu.Runtime.ExecutorSpawn

open Fable.Core
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Runtime.ExecutorSpawnHelper
open Wanxiangshu.Runtime.ExecutorPlatform
open Wanxiangshu.Runtime.RuntimeScope

type RunOutcome = Wanxiangshu.Runtime.ExecutorSpawnHelper.RunOutcome

let spawnAndRun
    (scope: RuntimeScope)
    (command: string)
    (args: string array)
    (cwd: string)
    (timeoutMs: int option)
    (sessionId: string option)
    (onKillRegistered: ((unit -> unit) -> unit) option)
    : JS.Promise<RunOutcome> =
    let child = spawnChild command args cwd
    awaitChild scope child command killTree timeoutMs sessionId onKillRegistered

let runScript
    (scope: RuntimeScope)
    (interpreter: string)
    (interpreterArgs: string array)
    (cwd: string)
    (scriptPath: string)
    (timeoutMs: int option)
    (sessionId: string option)
    (onKillRegistered: ((unit -> unit) -> unit) option)
    : JS.Promise<RunOutcome> =
    spawnAndRun
        scope
        interpreter
        (Array.append interpreterArgs [| scriptPath |])
        cwd
        timeoutMs
        sessionId
        onKillRegistered

let missingExecutableFor (language: ExecutorLanguage) : string =
    match language with
    | Python -> "uvx"
    | Javascript -> "npx"
    | Shell -> if isWindows () then "powershell.exe" else "bash"

let partialStdout (output: string) =
    if output = "" then
        "(no output before termination)"
    else
        output
