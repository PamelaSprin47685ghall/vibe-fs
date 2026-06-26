module Wanxiangshu.Tests.OmpPluginTestsAgentEnd

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.OmpPluginTestsHarness
open Wanxiangshu.Omp.Plugin
open Wanxiangshu.Shell.RunnerBackground
open Wanxiangshu.Shell.Dyn
module Dyn = Wanxiangshu.Shell.Dyn

let agentEndRunnerNudgeBeforeLoop () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    setRunnerJobStateForTest "session-1" "running"
    let ctx =
        createObj [
            "sessionManager",
                box(
                    createObj [
                        "getSessionId", box(fun () -> box "session-1")
                        "getEntries", box(fun () -> box [||])
                    ])
            "hasPendingMessages", box(fun () -> box false)
        ]
    do! invokeHandler h "agent_end" (createObj []) ctx
    equal "runner reminder type" "wanxiangshu-runner-reminder" (lastMessageCustomType h)
}

let agentEndLoopNudgeWhenActive () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let ctxLoop =
        createObj [
            "sessionManager", box(createObj [ "getSessionId", box(fun () -> box "session-2") ])
            "ui", box(createObj [ "notify", box(fun (_: obj) (_: obj) -> ()) ])
        ]
    do!
        emitJsExpr (eventHandler h "input", createObj [ "text", box "/loop do task" ], ctxLoop)
            "Promise.resolve($0($1, $2))"
        |> unbox<JS.Promise<unit>>
    let ctxEnd =
        createObj [
            "sessionManager",
                box(
                    createObj [
                        "getSessionId", box(fun () -> box "session-2")
                        "getEntries", box(fun () -> box [||])
                    ])
            "hasPendingMessages", box(fun () -> box false)
        ]
    do! invokeHandler h "agent_end" (createObj []) ctxEnd
    equal "loop reminder type" "wanxiangshu-loop-reminder" (lastMessageCustomType h)
}

let agentEndSkipsLoopNudgeWhenPendingMessages () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let ctxLoop =
        createObj [
            "sessionManager", box(createObj [ "getSessionId", box(fun () -> box "session-3") ])
            "ui", box(createObj [ "notify", box(fun (_: obj) (_: obj) -> ()) ])
        ]
    do!
        emitJsExpr (eventHandler h "input", createObj [ "text", box "/loop gated" ], ctxLoop)
            "Promise.resolve($0($1, $2))"
        |> unbox<JS.Promise<unit>>
    let countAfterLoop = h.messages.Count
    let ctxEnd =
        createObj [
            "sessionManager",
                box(
                    createObj [
                        "getSessionId", box(fun () -> box "session-3")
                        "getEntries", box(fun () -> box [||])
                    ])
            "hasPendingMessages", box(fun () -> box true)
        ]
    do! invokeHandler h "agent_end" (createObj []) ctxEnd
    check "no loop reminder when pending" (h.messages.Count = countAfterLoop)
}

let agentEndTodoNudgeWhenOpenPhases () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let sm =
        createObj [
            "getSessionId", box(fun () -> box "session-todo")
            "getEntries", box(fun () -> box (todoPhaseEntries ()))
        ]
    let ctxEnd =
        createObj [
            "sessionManager", box sm
            "hasPendingMessages", box(fun () -> box false)
        ]
    do! invokeHandler h "agent_end" (createObj []) ctxEnd
    equal "todo reminder type" "wanxiangshu-todo-reminder" (lastMessageCustomType h)
}

let runnerNudgePromptUsesExecutorToolNames () =
    let text = Wanxiangshu.Kernel.PromptFragments.runnerNudgePromptFor Wanxiangshu.Kernel.HostTools.omp
    check "runner nudge names executor_wait" (text.Contains "executor_wait")
    check "runner nudge names executor_abort" (text.Contains "executor_abort")
    check "runner nudge avoids legacy runner_wait" (not (text.Contains "runner_wait"))