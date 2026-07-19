module Wanxiangshu.Tests.OmpRunnerTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Runtime.RunnerBackground
open Wanxiangshu.Runtime.SessionExecutor
open Wanxiangshu.Runtime.RuntimeScope

let waitRunnerJobAfterAppendLog () =
    promise {
        let scope = RuntimeScope()
        clearRunnerLogsForTest scope
        let sessionId = "runner-wait-1"
        appendRunnerLog scope sessionId "line-one\nline-two"
        let! snippet = waitRunnerJob scope sessionId 10
        check "wait includes log" (snippet.Contains "line-two")
        check "wait not empty placeholder" (snippet <> "(no new output)")
    }

let setRunnerJobStateForTestHasRunning () =
    let scope = RuntimeScope()
    clearRunnerLogsForTest scope
    let sessionId = "runner-active-1"
    check "not running before" (not (hasRunningRunnerJob scope sessionId))
    registerActiveRunnerSession scope sessionId
    check "running after register" (hasRunningRunnerJob scope sessionId)

let abortRunnerJobClearsRunning () =
    let scope = RuntimeScope()
    clearRunnerLogsForTest scope
    let sessionId = "runner-abort-1"
    registerActiveRunnerSession scope sessionId
    check "running before abort" (hasRunningRunnerJob scope sessionId)
    abortRunnerJob scope sessionId |> ignore
    check "not running after abort" (not (hasRunningRunnerJob scope sessionId))

let cleanupRunnerJobClearsRunning () =
    promise {
        let scope = RuntimeScope()
        clearRunnerLogsForTest scope
        let sessionId = "runner-cleanup-1"
        registerActiveRunnerSession scope sessionId
        check "running before cleanup" (hasRunningRunnerJob scope sessionId)
        do! cleanupRunnerJob scope sessionId
        check "not running after cleanup" (not (hasRunningRunnerJob scope sessionId))
    }

let hasRunningWhenActiveExecutorRun () =
    let scope = RuntimeScope()
    clearRunnerLogsForTest scope
    let sessionId = "runner-exec-active-1"
    check "not running before register" (not (hasRunningRunnerJob scope sessionId))
    let kill = (fun () -> ())
    registerActiveRun scope sessionId kill
    check "running when executor active" (hasRunningRunnerJob scope sessionId)
    unregisterActiveRun scope sessionId kill

let abortExecutorRunClearsActive () =
    let scope = RuntimeScope()
    clearRunnerLogsForTest scope
    let sessionId = "runner-exec-abort-1"
    let mutable killed = false
    registerActiveRun scope sessionId (fun () -> killed <- true)
    check "running before abort" (hasRunningRunnerJob scope sessionId)
    abortExecutorRun scope sessionId
    check "kill invoked" killed
    check "not running after abort" (not (hasRunningRunnerJob scope sessionId))

let executorChildToolNamesMatchOmpSessionTools () =
    equal "executor child names length" 0 ompRunnerChildToolNames.Length
