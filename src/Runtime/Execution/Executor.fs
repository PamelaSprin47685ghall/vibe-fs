module Wanxiangshu.Runtime.Executor

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Runtime.ExecutorFormat
open Wanxiangshu.Runtime.ExecutorSpawn
open Wanxiangshu.Runtime.ExecutorSpawnRunners
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ExecutorSpawnHelper

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

[<Emit("performance.now()")>]
let private now () : float = jsNative

type RunOutcome = ExecutorSpawn.RunOutcome
type ExecuteDeps = ExecutorSpawnRunners.ExecuteDeps
let defaultExecuteDeps = ExecutorSpawnRunners.defaultExecuteDeps
let missingExecutableFor = ExecutorSpawn.missingExecutableFor

let abortExecutorRun (scope: RuntimeScope) (sessionId: string) : unit =
    Wanxiangshu.Runtime.SessionExecutor.abortExecutorRun scope sessionId

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
    (scope: RuntimeScope)
    (options: ExecuteOptions)
    (sessionId: string)
    (onKillRegistered: ((unit -> unit) -> unit) option)
    : JS.Promise<ExecuteResult> =
    promise {
        let deadline = ExecutionDeadline.start now (timeoutMs options.timeoutType)
        let timeout = timeoutMs options.timeoutType
        let cwd = defaultArg options.cwd (nodeProcess?cwd ())
        let program = prepareProgramForExecution options

        let! outcome =
            deps.runProgram scope program options.language options.dependencies cwd sessionId deadline onKillRegistered

        let output =
            match outcome with
            | Exited(_, stdout, stderr)
            | TimedOut(stdout, stderr)
            | Signaled(_, stdout, stderr) -> (stdout + stderr).Trim()
            | SpawnFailed _ -> ""

        return mapOutcome options timeout output outcome
    }

let execute (scope: RuntimeScope) (options: ExecuteOptions) (sessionId: string) : JS.Promise<ExecuteResult> =
    executeWith defaultExecuteDeps scope options sessionId None
