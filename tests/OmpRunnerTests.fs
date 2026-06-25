module VibeFs.Tests.OmpRunnerTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.OmpSessionTools
open VibeFs.Shell.RunnerBackground
open VibeFs.Shell.SessionExecutor

let private reset () = resetRunnerJobsForTesting ()

let waitRunnerJobAfterAppendLog () = promise {
    reset ()
    let sessionId = "runner-wait-1"
    appendRunnerLog sessionId "line-one\nline-two"
    let! snippet = waitRunnerJob sessionId 10
    check "wait includes log" (snippet.Contains "line-two")
    check "wait not empty placeholder" (snippet <> "(no new output)")
}

let setRunnerJobStateForTestHasRunning () =
    reset ()
    let sessionId = "runner-active-1"
    check "not running before" (not (hasRunningRunnerJob sessionId))
    setRunnerJobStateForTest sessionId "running"
    check "running after test state" (hasRunningRunnerJob sessionId)

let abortRunnerJobClearsRunning () =
    reset ()
    let sessionId = "runner-abort-1"
    setRunnerJobStateForTest sessionId "running"
    check "running before abort" (hasRunningRunnerJob sessionId)
    abortRunnerJob sessionId |> ignore
    check "not running after abort" (not (hasRunningRunnerJob sessionId))

let cleanupRunnerJobClearsRunning () = promise {
    reset ()
    let sessionId = "runner-cleanup-1"
    registerActiveRunnerSession sessionId
    check "running before cleanup" (hasRunningRunnerJob sessionId)
    do! cleanupRunnerJob sessionId
    check "not running after cleanup" (not (hasRunningRunnerJob sessionId))
}

let hasRunningWhenActiveExecutorRun () =
    reset ()
    let sessionId = "runner-exec-active-1"
    check "not running before register" (not (hasRunningRunnerJob sessionId))
    registerActiveRun sessionId (fun () -> ())
    check "running when executor active" (hasRunningRunnerJob sessionId)
    unregisterActiveRun sessionId

let abortExecutorRunClearsActive () =
    reset ()
    let sessionId = "runner-exec-abort-1"
    let mutable killed = false
    registerActiveRun sessionId (fun () -> killed <- true)
    check "running before abort" (hasRunningRunnerJob sessionId)
    abortExecutorRun sessionId
    check "kill invoked" killed
    check "not running after abort" (not (hasRunningRunnerJob sessionId))

let executorChildToolNamesMatchOmpSessionTools () =
    equal "executor child names length" ompRunnerChildToolNames.Length 2
    equal "executor child wait" "executor_wait" ompRunnerChildToolNames.[0]
    equal "executor child abort" "executor_abort" ompRunnerChildToolNames.[1]