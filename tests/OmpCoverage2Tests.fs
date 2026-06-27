module Wanxiangshu.Tests.OmpCoverage2Tests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.OmpPluginTestsHarness
open Wanxiangshu.Shell.Dyn
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Omp.SessionLifecycleHooks
open Wanxiangshu.Omp.KnowledgeGraph.Runtime
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Omp.NudgeRuntime
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Omp.MagicTodo

let fakeCtx (sessionId: string) (cwd: string) : obj =
    createObj [
        "sessionManager",
            box(createObj [
                "getSessionId", box(fun () -> box sessionId)
                "sessionId", box sessionId ])
        "cwd", box cwd ]

[<Emit("process.cwd()")>]
let processCwd () : string = jsNative

let fakeKGR (pi: obj) : OmpKnowledgeGraphRuntime =
    new OmpKnowledgeGraphRuntime(pi)

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

let recordsToBookkeeper_coderReturnsTrue () =
    check "coder returns true" (recordsToBookkeeper "coder")

let recordsToBookkeeper_readReturnsFalse () =
    check "read returns false" (not (recordsToBookkeeper "read"))

let isReadOnlyExecutor_roModeReturnsTrue () =
    let args = createObj [ "mode", box "ro" ]
    check "executor ro true" (isReadOnlyExecutor "executor" args)

let isReadOnlyExecutor_rwModeReturnsFalse () =
    let args = createObj [ "mode", box "rw" ]
    check "executor rw false" (not (isReadOnlyExecutor "executor" args))

let isReadOnlyExecutor_otherToolReturnsFalse () =
    let args = createObj [ "mode", box "ro" ]
    check "coder ro false" (not (isReadOnlyExecutor "coder" args))

let applyActiveToolFilterForMainSession_filtersChildOnly () =
    let h = createPiHarness ()
    let pi = piObject h
    let tools = unbox<string array> (Dyn.get h.hookStore "activeTools")
    // activeTools from harness includes child-only names: find, edit, write, lsp, fuzzy_find, fuzzy_grep, executor_wait, executor_abort, return_reviewer, search, glob, ast_edit, ast_grep, browser
    let childOnlyInActive =
        tools |> Array.filter (fun t -> Set.contains t childOnlySet)
    let subagentInActive =
        tools |> Array.filter (fun t -> Array.contains t ompSubagentToolNames)
    let hasSubagent = subagentInActive.Length > 0
    // Is main session if subagent tool present; filter removes child-only
    // Use a context that forces isMainSession path: inject subagent tool into active set
    let activeWithSubagent =
        [| yield! tools; yield "coder" |]
    h.hookStore?("activeTools") <- box activeWithSubagent
    promise {
        do! applyActiveToolFilterForMainSession pi (fakeCtx "main-1" "/tmp")
        let filtered = unbox<string array> (Dyn.get h.hookStore "activeTools")
        let childOnlyInFiltered = filtered |> Array.filter (fun t -> Set.contains t childOnlySet)
        check "child-only tools removed from main session" (childOnlyInFiltered.Length = 0)
        check "subagent tool kept" (Array.contains "coder" filtered)
    }

let toolCallHandler_missingWarnTddBlocks () =
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let kgr = fakeKGR pi
    let event = fakeEvent "coder" (createObj [])
    promise {
        let! result = toolCallHandler pi store kgr event (fakeCtx "s1" "/tmp")
        let block = Dyn.getValue<bool> result "block"
        check "block is true for missing warn_tdd" block
    }

let toolCallHandler_childOnlyToolBlockedInMainSession () =
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let kgr = fakeKGR pi
    // "find" is child-only; main session context has no child session id
    let event = fakeEvent "find" (createObj [])
    promise {
        let! result = toolCallHandler pi store kgr event (fakeCtx "main-session" "/tmp")
        let block = Dyn.getValue<bool> result "block"
        check "child-only tool blocked in main session" block
    }

let toolCallHandler_normalToolReturnsNone () =
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let kgr = fakeKGR pi
    // "coder" is NOT child-only
    let event = fakeEvent "coder" (createObj [ "warn_tdd", box "i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles" ])
    promise {
        let! result = toolCallHandler pi store kgr event (fakeCtx "s1" "/tmp")
        check "normal tool returns null" (Dyn.isNullish result)
    }

let turnStartHandler_noThrow () =
    let h = createPiHarness ()
    let pi = piObject h
    promise {
        do! turnStartHandler pi (createObj []) (fakeCtx "ts1" "/tmp")
        check "turnStartHandler completed" true
    }

let toolResultHandler_todowriteCapturesReport () =
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let kgr = fakeKGR pi
    let report = "## Completed Work\n- fixed bug"
    let event =
        createObj [
            "toolName", box "todowrite"
            "input", box(createObj [ "completedWorkReport", box report ])
            "callId", box "call-1"
            "content", box [| createObj [ "type", box "text"; "text", box "" ] |] ]
    promise {
        do! toolResultHandler pi store kgr event (fakeCtx "s1" "/tmp")
        // CaptureReport must have stored the report in the BacklogSession.
        let captured = BacklogSession(omp).TakeReport("call-1")
        check "report captured in BacklogSession" (captured = report.Trim())
        // TakeReport consumes the entry; a second take returns empty.
        let again = BacklogSession(omp).TakeReport("call-1")
        check "report consumed after take" (again = "")
    }

let toolResultHandler_bookkeeperToolStartsAppend () =
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let kgr = fakeKGR pi
    let event = fakeResultEvent "coder" "coded something"
    promise {
        do! toolResultHandler pi store kgr event (fakeCtx "bk1" (processCwd ()))
        check "bookkeeper toolResultHandler completed" true
        // Verify the bookkeeper was triggered via kgRuntime test hook
        let launches = kgr.TakeBookkeeperLaunchesForTesting()
        check "bookkeeper launch recorded" (launches.Length >= 1)
    }

let toolResultHandler_readOnlyExecutorNotRecorded () =
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let kgr = fakeKGR pi
    let event = fakeResultEvent "executor" "read file"
    // executor args with mode=ro
    let args = createObj [ "mode", box "ro" ]
    event?input <- box args
    promise {
        do! toolResultHandler pi store kgr event (fakeCtx "ro1" "/tmp")
        let launches = kgr.TakeBookkeeperLaunchesForTesting()
        check "read-only executor not recorded in bookkeeper" (launches.Length = 0)
    }

let sessionStartHandler_appliesFilterAndKgTools () =
    let h = createPiHarness ()
    let pi = piObject h
    let kgr = fakeKGR pi
    // Reset kg tools registered state
    Wanxiangshu.Omp.KnowledgeGraphTools.resetOmpKgToolsTestState ()
    promise {
        do! sessionStartHandler pi kgr (fakeCtx "ss1" "/tmp")
        // applyActiveToolFilterForMainSession must strip child-only tools
        // from the main session's active-tool list (harness starts with all
        // tools including child-only entries; after filter none should remain).
        let filtered = unbox<string array> (Dyn.get h.hookStore "activeTools")
        let childOnlyInFiltered = filtered |> Array.filter (fun t -> Set.contains t childOnlySet)
        check "child-only tools removed from main session" (childOnlyInFiltered.Length = 0)
    }

let sessionShutdownHandler_clearsState () =
    let h = createPiHarness ()
    let pi = piObject h
    let store = createReviewStore ()
    let kgr = fakeKGR pi
    // Register a job so DeleteJob has something to clear
    kgr.RegisterJobForTesting("sh1", "/tmp", "append", createObj [])
    let ctx = fakeCtx "sh1" "/tmp"
    promise {
        do! sessionShutdownHandler store kgr ctx
        // Verify nudge cleared
        Wanxiangshu.Omp.NudgeRuntime.clearNudgeSession "sh1" |> ignore
        // kgRuntime.DeleteJob must have removed sh1 from registered jobs.
        let snapshot = kgr.SnapshotRegisteredJobsForTesting()
        let sh1StillThere = snapshot |> Array.exists (fun (sid, _) -> sid = "sh1")
        check "sh1 job deleted from kgRuntime" (not sh1StillThere)
    }

let run () : JS.Promise<unit> =
    promise {
        clearFailuresForRun ()
        // 1. recordsToBookkeeper
        recordsToBookkeeper_coderReturnsTrue ()
        recordsToBookkeeper_readReturnsFalse ()
        // 2. isReadOnlyExecutor
        isReadOnlyExecutor_roModeReturnsTrue ()
        isReadOnlyExecutor_rwModeReturnsFalse ()
        isReadOnlyExecutor_otherToolReturnsFalse ()
        // 3. applyActiveToolFilterForMainSession
        do! applyActiveToolFilterForMainSession_filtersChildOnly ()
        // 4. toolCallHandler
        do! toolCallHandler_missingWarnTddBlocks ()
        do! toolCallHandler_childOnlyToolBlockedInMainSession ()
        do! toolCallHandler_normalToolReturnsNone ()
        // 5. turnStartHandler
        do! turnStartHandler_noThrow ()
        // 6. toolResultHandler
        do! toolResultHandler_todowriteCapturesReport ()
        do! toolResultHandler_bookkeeperToolStartsAppend ()
        do! toolResultHandler_readOnlyExecutorNotRecorded ()
        // 7. sessionStartHandler
        do! sessionStartHandler_appliesFilterAndKgTools ()
        // 8. sessionShutdownHandler
        do! sessionShutdownHandler_clearsState ()
    }
