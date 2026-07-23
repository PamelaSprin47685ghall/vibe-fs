module Wanxiangshu.Tests.OmpPluginTestsLifecycle

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.OmpPluginTestsHarness
open Wanxiangshu.Hosts.Omp.Plugin
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn

let extensionRegistersLifecycleHooks () =
    promise {
        resetPluginState ()
        let h = createPiHarness ()
        let pi = piObject h
        do! wanxiangshuExtension pi
        let events = Dyn.get h.hookStore "events"
        check "registers session_start hook" (Dyn.has events "session_start")
        check "registers before_agent_start hook" (Dyn.has events "before_agent_start")
        check "registers tool_call hook" (Dyn.has events "tool_call")
        check "registers tool_result hook" (Dyn.has events "tool_result")
        check "registers agent_end hook" (Dyn.has events "agent_end")
        check "registers session_shutdown hook" (Dyn.has events "session_shutdown")
        check "registers turn_start hook" (Dyn.has events "turn_start")
    }

let toolCallHookCanBeInvoked () =
    promise {
        resetPluginState ()
        let h = createPiHarness ()
        let pi = piObject h
        do! wanxiangshuExtension pi
        let handler = eventHandler h "tool_call"

        let event =
            createObj
                [ "toolName", box "coder"
                  "input", box (createObj [ "objective", box "test" ]) ]

        let! _ =
            emitJsExpr (handler, event, createObj [ "cwd", box "/tmp" ]) "Promise.resolve($0($1, $2))"
            |> unbox<JS.Promise<obj>>

        check "tool_call handler invoked without error" true
    }

let toolCallBlocksChildOnlyInMainSession () =
    promise {
        resetPluginState ()
        let h = createPiHarness ()
        let pi = piObject h
        do! wanxiangshuExtension pi
        let handler = eventHandler h "tool_call"
        let event = createObj [ "toolName", box "edit"; "input", box (createObj []) ]

        let ctx =
            createObj
                [ "sessionManager", box (createObj [ "getSessionId", box (fun () -> box "main-session") ])
                  "cwd", box "/tmp" ]

        let! result =
            emitJsExpr (handler, event, ctx) "Promise.resolve($0($1, $2))"
            |> unbox<JS.Promise<obj>>

        check "tool_call blocks edit in main session" (unbox<bool> (Dyn.get result "block"))
        check "block reason present" (Dyn.str result "reason" <> "")
    }

let turnStartRestoresMainSessionTools () =
    promise {
        resetPluginState ()
        let h = createPiHarness ()
        let pi = piObject h
        do! wanxiangshuExtension pi

        h.hookStore?("activeTools") <-
            box
                [| "read"
                   "edit"
                   "write"
                   "coder"
                   "inspector"
                   "meditator"
                   "executor"
                   "submit_review"
                   "return_reviewer"
                   "todowrite" |]

        let handler = eventHandler h "turn_start"

        do!
            emitJsExpr
                (handler, createObj [ "turnIndex", box 0 ], createObj [ "cwd", box "/tmp" ])
                "Promise.resolve($0($1, $2))"
            |> unbox<JS.Promise<unit>>

        let active = Set.ofArray (activeTools h)
        check "turn_start strips edit from main session" (not (active.Contains "edit"))
        check "turn_start keeps coder" (active.Contains "coder")
    }
