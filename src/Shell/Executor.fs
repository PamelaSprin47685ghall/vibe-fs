module Wanxiangshu.Shell.Executor

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Shell.ExecutorSpawn
open Wanxiangshu.Shell.ExecutorSpawnRunners

[<Global("process")>]
let private nodeProcess: obj = jsNative

type RunOutcome = ExecutorSpawn.RunOutcome
type ExecuteDeps = ExecutorSpawnRunners.ExecuteDeps
let defaultExecuteDeps = ExecutorSpawnRunners.defaultExecuteDeps
let missingExecutableFor = ExecutorSpawn.missingExecutableFor

let abortExecutorRun (sessionId: string) : unit =
    Wanxiangshu.Shell.SessionExecutor.abortExecutorRun sessionId

let mapOutcome (options: ExecuteOptions) (timeout: int) (output: string) (outcome: RunOutcome) : ExecuteResult =
    match outcome with
    | TimedOut _ -> Truncated(output = partialStdout output, timeoutType = options.timeoutType)
    | Signaled(signal, _, _) -> Failed(partialStdout output, None, Some signal)
    | Exited(0, _, _) ->
        let body = if output = "" then "(no output)" else output
        Completed(body, 0)
    | Exited(code, _, _) -> Failed(output, Some code, None)
    | SpawnFailed(ExecutorExecutableMissing exe) ->
        MissingExecutable(
            exe,
            $"Error: '{exe}' executable not found. Please ensure '{exe}' is installed and available on your PATH."
        )
    | SpawnFailed reason -> Failed($"spawn failed: {formatDomainError reason}", None, None)

let executeWith
    (deps: ExecuteDeps)
    (options: ExecuteOptions)
    (sessionId: string)
    (onKillRegistered: ((unit -> unit) -> unit) option)
    : JS.Promise<ExecuteResult> =
    promise {
        let timeout = timeoutMs options.timeoutType
        let cwd = defaultArg options.cwd (nodeProcess?cwd ())
        let program = prepareProgramForExecution options

        let! outcome =
            deps.runProgram program options.language options.dependencies cwd sessionId timeout onKillRegistered

        let output =
            match outcome with
            | Exited(_, stdout, stderr)
            | TimedOut(stdout, stderr)
            | Signaled(_, stdout, stderr) -> (stdout + stderr).Trim()
            | SpawnFailed _ -> ""

        return mapOutcome options timeout output outcome
    }

let execute (options: ExecuteOptions) (sessionId: string) : JS.Promise<ExecuteResult> =
    executeWith defaultExecuteDeps options sessionId None
