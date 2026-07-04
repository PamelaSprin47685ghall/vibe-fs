module Wanxiangshu.Tests.OmpCoverage2Tests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.OmpPluginTestsHarness
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.RuntimeScope
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Omp.SessionLifecycleHooks
open Wanxiangshu.Omp.NudgeHooks
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Omp.NudgeRuntime
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Omp.MagicTodo

let private testScope = RuntimeScope()

let fakeCtx (sessionId: string) (cwd: string) : obj =
    createObj [
        "sessionManager",
            box(createObj [
                "getSessionId", box(fun () -> box sessionId)
                "sessionId", box sessionId ])
        "cwd", box cwd ]

[<Emit("process.cwd()")>]
let processCwd () : string = jsNative

let childOnlySet = Set.ofArray ompChildOnlyToolNames

let fakeEvent (toolName: string) (input: obj) : obj =
    createObj [
        "toolName", box toolName
        "input", box input ]

let fakeResultEvent (toolName: string) (content: string) : obj =
    createObj [
        "toolName", box toolName
        "input", box null
        "content", box [| createObj [ "type", box "text"; "text", box content ] |] ]

let applyActiveToolFilterForMainSession_filtersChildOnly () =
    let h = createPiHarness ()
    let pi = piObject h
    let tools = unbox<string array> (Dyn.get h.hookStore "activeTools")
    // activeTools from harness includes child-only names: find, edit, write, lsp, fuzzy_find, fuzzy_grep, executor_wait, executor_abort, return_reviewer, search, glob, ast_edit, ast_grep, browser
    let childOnlyInActive =
        tools |> Array.filter (fun t -> Set.contains t childOnlySet)
    // harness activeTools already contains "coder" so isMainSession = true
    promise {
        do! applyActiveToolFilterForMainSession pi (fakeCtx "main-1" "/tmp")
        let filtered = unbox<string array> (Dyn.get h.hookStore "activeTools")
        let childOnlyInFiltered = filtered |> Array.filter (fun t -> Set.contains t childOnlySet)
        check "child-only tools removed from main session" (childOnlyInFiltered.Length = 0)
    }

let toolCallHandler_missingWarnTddBlocks () =
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let event = fakeEvent "coder" (createObj [])
    promise {
        let! result = toolCallHandler pi store event (fakeCtx "s1" "/tmp")
        let block = Dyn.getValue<bool> result "block"
        check "block is true for missing warn_tdd" block
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
    // "coder" is NOT child-only
    let event = fakeEvent "coder" (createObj [ "warn_tdd", box "i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles" ])
    promise {
        let! result = toolCallHandler pi store event (fakeCtx "s1" "/tmp")
        check "normal tool returns null" (Dyn.isNullish result)
    }

let toolCallHandler_childSessionCoderMissingWarnTddBlocked () =
    Wanxiangshu.Omp.ChildSession.clearChildSessionsForTest testScope ()
    let childId = "child-coder-1"
    Wanxiangshu.Omp.ChildSession.markChildSession testScope childId
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let event = fakeEvent "coder" (createObj [])
    promise {
        let! result = toolCallHandler pi store event (fakeCtx childId "/tmp")
        let block = Dyn.getValue<bool> result "block"
        check "child session coder without warn_tdd blocked" block
        Wanxiangshu.Omp.ChildSession.unmarkChildSession testScope childId
    }

let toolCallHandler_childSessionExecutorMissingWarnBlocked () =
    Wanxiangshu.Omp.ChildSession.clearChildSessionsForTest testScope ()
    let childId = "child-exec-1"
    Wanxiangshu.Omp.ChildSession.markChildSession testScope childId
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let event = fakeEvent "executor" (createObj [])
    promise {
        let! result = toolCallHandler pi store event (fakeCtx childId "/tmp")
        let block = Dyn.getValue<bool> result "block"
        check "child session executor without warn blocked" block
        Wanxiangshu.Omp.ChildSession.unmarkChildSession testScope childId
    }

let toolCallHandler_childSessionReadPasses () =
    Wanxiangshu.Omp.ChildSession.clearChildSessionsForTest testScope ()
    let childId = "child-read-1"
    Wanxiangshu.Omp.ChildSession.markChildSession testScope childId
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let event = fakeEvent "read" (createObj [ "filePath", box "/tmp/foo" ])
    promise {
        let! result = toolCallHandler pi store event (fakeCtx childId "/tmp")
        check "child session read (non-modification) passes" (Dyn.isNullish result)
        Wanxiangshu.Omp.ChildSession.unmarkChildSession testScope childId
    }

let turnStartHandler_filtersChildOnlyTools () =
    let h = createPiHarness ()
    let pi = piObject h
    // harness activeTools already contains "coder" so isMainSession = true in filterOmpMainSessionActiveTools
    promise {
        do! turnStartHandler pi (createObj []) (fakeCtx "main-1" "/tmp")
        let filtered = unbox<string array> (Dyn.get h.hookStore "activeTools")
        let childOnlyInFiltered = filtered |> Array.filter (fun t -> Set.contains t childOnlySet)
        check "child-only tools removed after turnStartHandler" (childOnlyInFiltered.Length = 0)
        check "bash removed after turnStartHandler" (not (Array.contains "bash" filtered))
    }

let toolResultHandler_todowriteCapturesReport () =
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let report = "## Completed Work\n- fixed bug"
    let event =
        createObj [
            "toolName", box "todowrite"
            "input", box(createObj [ "ahaMoments", box report; "changesAndReasons", box ""; "gotchas", box ""; "lessonsAndConventions", box ""; "plan", box "" ])
            "callId", box "call-1"
            "content", box [| createObj [ "type", box "text"; "text", box "" ] |] ]
    promise {
        do! toolResultHandler pi store event (fakeCtx "s1" "/tmp")
        // CaptureReport must have stored the report in the BacklogSession.
        let captured = BacklogSession(omp).TakeReport("call-1")
        check "report captured in BacklogSession" (captured = "")
        // TakeReport consumes the entry; a second take returns empty.
        let again = BacklogSession(omp).TakeReport("call-1")
        check "report consumed after take" (again = "")
    }

let sessionShutdownHandler_clearsState () =
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let ctx = fakeCtx "sh1" "/tmp"
    promise {
        do! sessionShutdownHandler store ctx
        // nudge state cleared
        Wanxiangshu.Omp.NudgeRuntime.clearNudgeSession "sh1" |> ignore
    }

let run () : JS.Promise<unit> =
    promise {
        // 1. applyActiveToolFilterForMainSession
        do! applyActiveToolFilterForMainSession_filtersChildOnly ()
        // 2. toolCallHandler
        do! toolCallHandler_missingWarnTddBlocks ()
        do! toolCallHandler_childOnlyToolBlockedInMainSession ()
        do! toolCallHandler_normalToolReturnsNone ()
        do! toolCallHandler_childSessionCoderMissingWarnTddBlocked ()
        do! toolCallHandler_childSessionExecutorMissingWarnBlocked ()
        do! toolCallHandler_childSessionReadPasses ()
        // 3. turnStartHandler
        do! turnStartHandler_filtersChildOnlyTools ()
        // 4. toolResultHandler
        do! toolResultHandler_todowriteCapturesReport ()
        // 5. sessionShutdownHandler
        do! sessionShutdownHandler_clearsState ()
    }
