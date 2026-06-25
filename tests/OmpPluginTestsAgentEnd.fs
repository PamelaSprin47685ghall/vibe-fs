module VibeFs.Tests.OmpPluginTestsAgentEnd

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.OmpPluginTestsHarness
open VibeFs.Omp.Plugin
open VibeFs.Shell.RunnerBackground
open VibeFs.Shell.Dyn
module Dyn = VibeFs.Shell.Dyn

let agentEndRunnerNudgeBeforeLoop () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! kunweiExtension pi
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
    check "Opencode parity: no runner nudge on agent_end" (h.messages.Count = 0)
}

let agentEndLoopNudgeWhenActive () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! kunweiExtension pi
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
    equal "loop reminder type" "kunwei-loop-reminder" (lastMessageCustomType h)
}

let agentEndSkipsLoopNudgeWhenPendingMessages () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! kunweiExtension pi
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
    do! kunweiExtension pi
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
    equal "todo reminder type" "kunwei-todo-reminder" (lastMessageCustomType h)
}

let runnerNudgePromptUsesExecutorToolNames () =
    let text = VibeFs.Kernel.PromptFragments.runnerNudgePromptFor VibeFs.Kernel.HostTools.omp
    check "runner nudge names executor_wait" (text.Contains "executor_wait")
    check "runner nudge names executor_abort" (text.Contains "executor_abort")
    check "runner nudge avoids legacy runner_wait" (not (text.Contains "runner_wait"))