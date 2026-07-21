module Wanxiangshu.Tests.OmpCoverage2Tests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.OmpPluginTestsHarness
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.RuntimeScope

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Hosts.Omp.NudgeToolFilter
open Wanxiangshu.Hosts.Omp.NudgeHooks
open Wanxiangshu.Hosts.Omp.TodoHooks
open Wanxiangshu.Hosts.Omp.TodoStateManagement
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Hosts.Omp.NudgeRuntime
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Hosts.Omp.ExecutorTools
open Wanxiangshu.Runtime.Fallback.RuntimeStore

let fakeCtx (sessionId: string) (cwd: string) : obj =
    createObj
        [ "sessionManager",
          box (createObj [ "getSessionId", box (fun () -> box sessionId); "sessionId", box sessionId ])
          "cwd", box cwd ]

[<Emit("process.cwd()")>]
let processCwd () : string = jsNative

let childOnlySet = Set.ofArray ompChildOnlyToolNames

let fakeEvent (toolName: string) (input: obj) : obj =
    createObj [ "toolName", box toolName; "input", box input; "toolCallId", box "c1" ]

let fakeResultEvent (toolName: string) (content: string) : obj =
    createObj
        [ "toolName", box toolName
          "input", box null
          "content", box [| createObj [ "type", box "text"; "text", box content ] |] ]

let applyActiveToolFilterForMainSession_filtersChildOnly () =
    let h = createPiHarness ()
    let pi = piObject h
    let tools = unbox<string array> (Dyn.get h.hookStore "activeTools")
    // activeTools from harness includes child-only names: find, edit, write, lsp, fuzzy_find, fuzzy_grep, executor_wait, executor_abort, return_reviewer, search, glob, ast_edit, ast_grep, browser
    let childOnlyInActive = tools |> Array.filter (fun t -> Set.contains t childOnlySet)
    // harness activeTools already contains "coder" so isMainSession = true
    promise {
        do! applyActiveToolFilterForMainSession pi (fakeCtx "main-1" "/tmp")
        let filtered = unbox<string array> (Dyn.get h.hookStore "activeTools")

        let childOnlyInFiltered =
            filtered |> Array.filter (fun t -> Set.contains t childOnlySet)

        check "child-only tools removed from main session" (childOnlyInFiltered.Length = 0)
    }

let toolCallHandler_coderWithoutTddAckDoesNotBlock () =
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let event = fakeEvent "coder" (createObj [])

    promise {
        Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance "s1"
        let! result = toolCallHandler pi store event (fakeCtx "s1" "/tmp")
        check "coder without tdd ack does not block" (Dyn.isNullish result)
        let comp = Wanxiangshu.Runtime.ToolHookRuntime.tryGetCompliance "s1" "c1"
        check "coder without tdd ack has compliance entry" comp.IsSome
        check "compliance entry has no violations" (comp.Value.Violations.IsEmpty)
    }

let toolCallHandler_childOnlyToolBlockedInMainSession () =
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    // "find" is child-only; main session context has no child session id
    let event = fakeEvent "find" (createObj [])

    promise {
        let! result = toolCallHandler pi store event (fakeCtx "main-session" "/tmp")
        let block = Dyn.getValue<bool> result "block"
        check "child-only tool blocked in main session" block
    }

let toolCallHandler_normalToolReturnsNone () =
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let event = fakeEvent "coder" (createObj [])

    promise {
        let! result = toolCallHandler pi store event (fakeCtx "s1" "/tmp")
        check "normal tool returns null" (Dyn.isNullish result)
    }

let toolCallHandler_childSessionCoderWithoutTddAckDoesNotBlock () =
    let scope = RuntimeScope()
    Wanxiangshu.Hosts.Omp.ChildSession.clearChildSessionsForTest scope ()
    let childId = "child-coder-1"
    Wanxiangshu.Hosts.Omp.ChildSession.markChildSession scope childId
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let event = fakeEvent "coder" (createObj [])

    promise {
        Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance childId
        let! result = toolCallHandler pi store event (fakeCtx childId "/tmp")
        check "child session coder without tdd ack does not block" (Dyn.isNullish result)
        let comp = Wanxiangshu.Runtime.ToolHookRuntime.tryGetCompliance childId "c1"
        check "child session coder without tdd ack has compliance entry" comp.IsSome
        check "compliance entry child session has no violations" (comp.Value.Violations.IsEmpty)
        Wanxiangshu.Hosts.Omp.ChildSession.unmarkChildSession scope childId
    }

let toolCallHandler_childSessionExecutorMissingWarnBlocked () =
    let scope = RuntimeScope()
    Wanxiangshu.Hosts.Omp.ChildSession.clearChildSessionsForTest scope ()
    let childId = "child-exec-1"
    Wanxiangshu.Hosts.Omp.ChildSession.markChildSession scope childId
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let event = fakeEvent "executor" (createObj [])

    promise {
        Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance childId
        let! result = toolCallHandler pi store event (fakeCtx childId "/tmp")
        check "child session executor without warn does not block" (Dyn.isNullish result)
        let comp = Wanxiangshu.Runtime.ToolHookRuntime.tryGetCompliance childId "c1"
        check "missing warn child session has compliance entry" comp.IsSome
        check "compliance entry child session executor has no violations" (comp.Value.Violations.IsEmpty)
        Wanxiangshu.Hosts.Omp.ChildSession.unmarkChildSession scope childId
    }

let toolCallHandler_childSessionReadPasses () =
    let scope = RuntimeScope()
    Wanxiangshu.Hosts.Omp.ChildSession.clearChildSessionsForTest scope ()
    let childId = "child-read-1"
    Wanxiangshu.Hosts.Omp.ChildSession.markChildSession scope childId
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let event = fakeEvent "read" (createObj [ "filePath", box "/tmp/foo" ])

    promise {
        let! result = toolCallHandler pi store event (fakeCtx childId "/tmp")
        check "child session read (non-modification) passes" (Dyn.isNullish result)
        Wanxiangshu.Hosts.Omp.ChildSession.unmarkChildSession scope childId
    }

let turnStartHandler_filtersChildOnlyTools () =
    let h = createPiHarness ()
    let pi = piObject h
    // harness activeTools already contains "coder" so isMainSession = true in filterOmpMainSessionActiveTools
    promise {
        let rt = FallbackRuntimeStore()
        do! turnStartHandler pi (createObj []) (fakeCtx "main-1" "/tmp") rt
        let filtered = unbox<string array> (Dyn.get h.hookStore "activeTools")

        let childOnlyInFiltered =
            filtered |> Array.filter (fun t -> Set.contains t childOnlySet)

        check "child-only tools removed after turnStartHandler" (childOnlyInFiltered.Length = 0)
        check "bash removed after turnStartHandler" (not (Array.contains "bash" filtered))
    }

let toolResultHandler_capturesReport () = promise { return () }

let sessionShutdownHandler_clearsState () =
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let fallbackRuntime = FallbackRuntimeStore()
    let ctx = fakeCtx "sh1" "/tmp"

    promise {
        do! sessionShutdownHandler store fallbackRuntime ctx
        // nudge state cleared
        Wanxiangshu.Hosts.Omp.NudgeRuntime.clearNudgeSession fallbackRuntime "sh1"
        |> ignore
    }

let run () : JS.Promise<unit> =
    promise {
        do! applyActiveToolFilterForMainSession_filtersChildOnly ()
        do! toolCallHandler_coderWithoutTddAckDoesNotBlock ()
        do! toolCallHandler_childOnlyToolBlockedInMainSession ()
        do! toolCallHandler_normalToolReturnsNone ()
        do! toolCallHandler_childSessionCoderWithoutTddAckDoesNotBlock ()
        do! toolCallHandler_childSessionExecutorMissingWarnBlocked ()
        do! toolCallHandler_childSessionReadPasses ()
        do! turnStartHandler_filtersChildOnlyTools ()
        do! toolResultHandler_capturesReport ()
        do! sessionShutdownHandler_clearsState ()
    }
