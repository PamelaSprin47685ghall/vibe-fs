module Wanxiangshu.Tests.OmpPluginTestsAgentEnd

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.OmpPluginTestsHarness
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Hosts.Omp
open Wanxiangshu.Hosts.Omp.Plugin
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Runtime.PromptFrontMatter

module Dyn = Wanxiangshu.Runtime.Dyn

let agentEndRunnerNudgeBeforeLoop () =
    promise {
        resetPluginState ()
        let h = createPiHarness ()
        let pi = piObject h
        do! wanxiangshuExtension pi
        RunnerBackground.registerActiveRunnerSession ExecutorTools.ompScope "session-1"

        let assistantEntry =
            createObj
                [ "message",
                  box (
                      createObj
                          [ "role", box "assistant"
                            "content",
                            box [| createObj [ "type", box "text"; "text", box "waiting on background runner" ] |] ]
                  ) ]

        let! workspaceDir = mkdtempAsync "omp-agent-end-runner-"

        let ctx =
            createObj
                [ "sessionManager",
                  box (
                      createObj
                          [ "getSessionId", box (fun () -> box "session-1")
                            "getEntries", box (fun () -> box [| assistantEntry |]) ]
                  )
                  "hasPendingMessages", box (fun () -> box false)
                  "cwd", box workspaceDir ]

        do! invokeHandler h "agent_end" (createObj []) ctx
        equal "runner reminder type" "wanxiangshu-runner-reminder" (lastMessageCustomType h)
        do! rmAsync workspaceDir
    }

let agentEndLoopNudgeWhenActive () =
    promise {
        resetPluginState ()
        let h = createPiHarness ()
        let pi = piObject h
        do! wanxiangshuExtension pi
        let! workspaceDir = mkdtempAsync "omp-agent-end-loop-"

        let ctxLoop =
            createObj
                [ "sessionManager", box (createObj [ "getSessionId", box (fun () -> box "session-2") ])
                  "ui", box (createObj [ "notify", box (fun (_: obj) (_: obj) -> ()) ])
                  "cwd", box workspaceDir ]

        do!
            emitJsExpr
                (eventHandler h "input", createObj [ "text", box "/loop do task" ], ctxLoop)
                "Promise.resolve($0($1, $2))"
            |> unbox<JS.Promise<unit>>

        let ctxEnd =
            createObj
                [ "sessionManager",
                  box (
                      createObj
                          [ "getSessionId", box (fun () -> box "session-2")
                            "getEntries",
                            box (fun () ->
                                box
                                    [| createObj
                                           [ "message",
                                             box (
                                                 createObj
                                                     [ "role", box "assistant"
                                                       "content",
                                                       box
                                                           [| createObj
                                                                  [ "type", box "text"
                                                                    "text",
                                                                    box (
                                                                        frontMatterPrompt
                                                                            [ yamlField taskField "do task" ]
                                                                            "With-Review Mode is active."
                                                                    ) ] |] ]
                                             ) ] |]) ]
                  )
                  "hasPendingMessages", box (fun () -> box false)
                  "cwd", box workspaceDir ]

        do! invokeHandler h "agent_end" (createObj []) ctxEnd
        equal "loop reminder type" "wanxiangshu-loop-reminder" (lastMessageCustomType h)
        do! rmAsync workspaceDir
    }

let agentEndSkipsLoopNudgeWithoutWorkerTaskAnchor () =
    promise {
        resetPluginState ()
        let h = createPiHarness ()
        let pi = piObject h
        do! wanxiangshuExtension pi

        let reviewerOnly =
            Wanxiangshu.Runtime.ReviewPrompts.Submission.reviewerPrompt "do task" "" []

        let! workspaceDir = mkdtempAsync "omp-agent-end-skips-"

        let ctxEnd =
            createObj
                [ "sessionManager",
                  box (
                      createObj
                          [ "getSessionId", box (fun () -> box "session-2b")
                            "getEntries",
                            box (fun () ->
                                box
                                    [| createObj
                                           [ "message",
                                             box (
                                                 createObj
                                                     [ "role", box "assistant"
                                                       "content",
                                                       box
                                                           [| createObj
                                                                  [ "type", box "text"; "text", box reviewerOnly ] |] ]
                                             ) ] |]) ]
                  )
                  "hasPendingMessages", box (fun () -> box false)
                  "cwd", box workspaceDir ]

        do! invokeHandler h "agent_end" (createObj []) ctxEnd
        check "reviewer-only history does not emit loop reminder" (h.messages.Count = 0)
        do! rmAsync workspaceDir
    }

let agentEndSkipsLoopNudgeWhenPendingMessages () =
    promise {
        resetPluginState ()
        let h = createPiHarness ()
        let pi = piObject h
        do! wanxiangshuExtension pi
        let! workspaceDir = mkdtempAsync "omp-agent-end-pending-"

        let ctxLoop =
            createObj
                [ "sessionManager", box (createObj [ "getSessionId", box (fun () -> box "session-3") ])
                  "ui", box (createObj [ "notify", box (fun (_: obj) (_: obj) -> ()) ])
                  "cwd", box workspaceDir ]

        do!
            emitJsExpr
                (eventHandler h "input", createObj [ "text", box "/loop gated" ], ctxLoop)
                "Promise.resolve($0($1, $2))"
            |> unbox<JS.Promise<unit>>

        let countAfterLoop = h.messages.Count

        let ctxEnd =
            createObj
                [ "sessionManager",
                  box (
                      createObj
                          [ "getSessionId", box (fun () -> box "session-3")
                            "getEntries", box (fun () -> box [||]) ]
                  )
                  "hasPendingMessages", box (fun () -> box true)
                  "cwd", box workspaceDir ]

        do! invokeHandler h "agent_end" (createObj []) ctxEnd
        check "no loop reminder when pending" (h.messages.Count = countAfterLoop)
        do! rmAsync workspaceDir
    }

let agentEndTodoNudgeWhenOpenPhases () =
    promise {
        resetPluginState ()
        let h = createPiHarness ()
        let pi = piObject h
        do! wanxiangshuExtension pi

        let assistantEntry =
            createObj
                [ "message",
                  box (
                      createObj
                          [ "role", box "assistant"
                            "content", box [| createObj [ "type", box "text"; "text", box "paused with open todos" ] |] ]
                  ) ]

        let sm =
            createObj
                [ "getSessionId", box (fun () -> box "session-todo")
                  "getEntries", box (fun () -> Array.append (todoPhaseEntries ()) [| assistantEntry |]) ]

        let! workspaceDir = mkdtempAsync "omp-agent-end-todo-"

        let ctxEnd =
            createObj
                [ "sessionManager", box sm
                  "hasPendingMessages", box (fun () -> box false)
                  "cwd", box workspaceDir ]

        do! invokeHandler h "agent_end" (createObj []) ctxEnd
        equal "todo reminder type" "wanxiangshu-todo-reminder" (lastMessageCustomType h)
        do! rmAsync workspaceDir
    }

let run () =
    promise {
        do! agentEndRunnerNudgeBeforeLoop ()
        do! agentEndLoopNudgeWhenActive ()
        do! agentEndSkipsLoopNudgeWithoutWorkerTaskAnchor ()
        do! agentEndSkipsLoopNudgeWhenPendingMessages ()
        do! agentEndTodoNudgeWhenOpenPhases ()
    }
