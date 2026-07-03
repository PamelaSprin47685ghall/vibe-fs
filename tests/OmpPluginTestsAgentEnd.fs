module Wanxiangshu.Tests.OmpPluginTestsAgentEnd

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.OmpPluginTestsHarness
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Omp.Plugin
open Wanxiangshu.Shell.RunnerBackground
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Kernel.PromptFrontMatter
module Dyn = Wanxiangshu.Shell.Dyn

let agentEndRunnerNudgeBeforeLoop () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    setRunnerJobStateForTest "session-1" "running"
    let assistantEntry =
        createObj [
            "message", box (createObj [
                "role", box "assistant"
                "content", box [| createObj [ "type", box "text"; "text", box "waiting on background runner" ] |]
            ])
        ]
    let! workspaceDir = mkdtempAsync "omp-agent-end-runner-"
    let ctx =
        createObj [
            "sessionManager",
                box(
                    createObj [
                        "getSessionId", box(fun () -> box "session-1")
                        "getEntries", box(fun () -> box [| assistantEntry |])
                    ])
            "hasPendingMessages", box(fun () -> box false)
            "cwd", box workspaceDir
        ]
    do! invokeHandler h "agent_end" (createObj []) ctx
    equal "runner reminder type" "wanxiangshu-runner-reminder" (lastMessageCustomType h)
    do! rmAsync workspaceDir
}

let agentEndLoopNudgeWhenActive () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let! workspaceDir = mkdtempAsync "omp-agent-end-loop-"
    let ctxLoop =
        createObj [
            "sessionManager", box(createObj [ "getSessionId", box(fun () -> box "session-2") ])
            "ui", box(createObj [ "notify", box(fun (_: obj) (_: obj) -> ()) ])
            "cwd", box workspaceDir
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
                        "getEntries", box(fun () -> box [| createObj [ "message", box (createObj [ "role", box "assistant"; "content", box [| createObj [ "type", box "text"; "text", box (frontMatterPrompt [ yamlField taskField "do task" ] "With-Review Mode is active.") ] |] ]) ] |])
                    ])
            "hasPendingMessages", box(fun () -> box false)
            "cwd", box workspaceDir
        ]
    do! invokeHandler h "agent_end" (createObj []) ctxEnd
    equal "loop reminder type" "wanxiangshu-loop-reminder" (lastMessageCustomType h)
    do! rmAsync workspaceDir
}

let agentEndSkipsLoopNudgeWithoutWorkerTaskAnchor () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let reviewerOnly = Wanxiangshu.Kernel.ReviewPrompts.Submission.reviewerPrompt "do task" "" []
    let ctxEnd =
        createObj [
            "sessionManager",
                box(
                    createObj [
                        "getSessionId", box(fun () -> box "session-2b")
                        "getEntries", box(fun () -> box [| createObj [ "message", box (createObj [ "role", box "assistant"; "content", box [| createObj [ "type", box "text"; "text", box reviewerOnly ] |] ]) ] |])
                    ])
            "hasPendingMessages", box(fun () -> box false)
        ]
    do! invokeHandler h "agent_end" (createObj []) ctxEnd
    check "reviewer-only history does not emit loop reminder" (h.messages.Count = 0)
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
    let assistantEntry =
        createObj [
            "message", box (createObj [
                "role", box "assistant"
                "content", box [| createObj [ "type", box "text"; "text", box "paused with open todos" ] |]
            ])
        ]
    let sm =
        createObj [
            "getSessionId", box(fun () -> box "session-todo")
            "getEntries", box(fun () -> Array.append (todoPhaseEntries ()) [| assistantEntry |])
        ]
    let! workspaceDir = mkdtempAsync "omp-agent-end-todo-"
    let ctxEnd =
        createObj [
            "sessionManager", box sm
            "hasPendingMessages", box(fun () -> box false)
            "cwd", box workspaceDir
        ]
    do! invokeHandler h "agent_end" (createObj []) ctxEnd
    equal "todo reminder type" "wanxiangshu-todo-reminder" (lastMessageCustomType h)
    do! rmAsync workspaceDir
}

let runnerNudgePromptUsesExecutorToolNames () =
    let text = Wanxiangshu.Kernel.PromptFragments.runnerNudgePromptFor Wanxiangshu.Kernel.HostTools.omp
    check "runner nudge names executor_wait" (text.Contains "executor_wait")
    check "runner nudge names executor_abort" (text.Contains "executor_abort")
    check "runner nudge avoids legacy runner_wait" (not (text.Contains "runner_wait"))

let run () = promise {
    do! agentEndRunnerNudgeBeforeLoop ()
    do! agentEndLoopNudgeWhenActive ()
    do! agentEndSkipsLoopNudgeWithoutWorkerTaskAnchor ()
    do! agentEndSkipsLoopNudgeWhenPendingMessages ()
    do! agentEndTodoNudgeWhenOpenPhases ()
    runnerNudgePromptUsesExecutorToolNames ()
}
