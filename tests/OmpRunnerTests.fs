module Wanxiangshu.Tests.OmpRunnerTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Shell.RunnerBackground
open Wanxiangshu.Shell.SessionExecutor
open Wanxiangshu.Shell.RuntimeScope

let testScope = RuntimeScope()

let private reset () = clearRunnerLogsForTest testScope

let waitRunnerJobAfterAppendLog () =
    promise {
        reset ()
        let sessionId = "runner-wait-1"
        appendRunnerLog testScope sessionId "line-one\nline-two"
        let! snippet = waitRunnerJob testScope sessionId 10
        check "wait includes log" (snippet.Contains "line-two")
        check "wait not empty placeholder" (snippet <> "(no new output)")
    }

let setRunnerJobStateForTestHasRunning () =
    reset ()
    let sessionId = "runner-active-1"
    check "not running before" (not (hasRunningRunnerJob testScope sessionId))
    registerActiveRunnerSession testScope sessionId
    check "running after register" (hasRunningRunnerJob testScope sessionId)

let abortRunnerJobClearsRunning () =
    reset ()
    let sessionId = "runner-abort-1"
    registerActiveRunnerSession testScope sessionId
    check "running before abort" (hasRunningRunnerJob testScope sessionId)
    abortRunnerJob testScope sessionId |> ignore
    check "not running after abort" (not (hasRunningRunnerJob testScope sessionId))

let cleanupRunnerJobClearsRunning () =
    promise {
        reset ()
        let sessionId = "runner-cleanup-1"
        registerActiveRunnerSession testScope sessionId
        check "running before cleanup" (hasRunningRunnerJob testScope sessionId)
        do! cleanupRunnerJob testScope sessionId
        check "not running after cleanup" (not (hasRunningRunnerJob testScope sessionId))
    }

let hasRunningWhenActiveExecutorRun () =
    reset ()
    let sessionId = "runner-exec-active-1"
    check "not running before register" (not (hasRunningRunnerJob testScope sessionId))
    registerActiveRun sessionId (fun () -> ())
    check "running when executor active" (hasRunningRunnerJob testScope sessionId)
    unregisterActiveRun sessionId

let abortExecutorRunClearsActive () =
    reset ()
    let sessionId = "runner-exec-abort-1"
    let mutable killed = false
    registerActiveRun sessionId (fun () -> killed <- true)
    check "running before abort" (hasRunningRunnerJob testScope sessionId)
    abortExecutorRun sessionId
    check "kill invoked" killed
    check "not running after abort" (not (hasRunningRunnerJob testScope sessionId))

let executorChildToolNamesMatchOmpSessionTools () =
    equal "executor child names length" 0 ompRunnerChildToolNames.Length
