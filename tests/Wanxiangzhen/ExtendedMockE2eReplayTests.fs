module Wanxiangshu.Tests.Wanxiangzhen.ExtendedMockE2eReplayTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorRuntime
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorReplay
open Wanxiangshu.Tests.Wanxiangzhen.AssertCompat
open Wanxiangshu.Tests.Wanxiangzhen.TestDoubles
open Wanxiangshu.Tests.Wanxiangzhen.ExtendedMockE2eFixtures

let testChatMessageCapturesSessionIdAndReplays () : JS.Promise<unit> =
    promise {
        let s = mkFake ()
        let deps = mkDeps s
        let rt = mkRuntime deps

        let sessionId = "squad-session-001"
        let evt1 = SquadCreated(sessionId, "add remember-me")

        let evt2 =
            TasksCreated(
                sessionId,
                [ { taskId = "squad-a1b2"
                    title = "Task A"
                    description = "desc A"
                    dependsOn = [] } ]
            )

        let history = [ evt1; evt2 ]

        s.getLatestSquadSessionIdOverride <- Some(fun () -> Promise.lift (Some sessionId))

        s.getSquadDagOverride <-
            Some(fun sid ->
                let dag = List.fold foldEvent (empty sid "") history
                Promise.lift dag)

        rt.MasterSessionId <- sessionId
        do! replayFromEventLog rt

        checkBare (rt.MasterSessionId = sessionId)

        match findTask "squad-a1b2" rt.Dag with
        | None -> checkBare false
        | Some t -> checkBare (t.Status = Pending)
    }

let testReplayReconcilesSubmittedToMerged () : JS.Promise<unit> =
    promise {
        let s = mkFake ()
        let deps = mkDeps s
        let rt = mkRuntime deps

        let sessionId = "squad-session-001"
        let evt1 = SquadCreated(sessionId, "req")

        let evt2 =
            TasksCreated(
                sessionId,
                [ { taskId = "squad-a1b2"
                    title = "A"
                    description = "desc"
                    dependsOn = [] } ]
            )

        let evt3 = TaskStarted(sessionId, "squad-a1b2", "/wt/a", "squad-a1b2")
        let evt4 = TaskSubmitted(sessionId, "squad-a1b2", "sha123")
        let history = [ evt1; evt2; evt3; evt4 ]

        s.getLatestSquadSessionIdOverride <- Some(fun () -> Promise.lift (Some sessionId))

        s.getSquadDagOverride <-
            Some(fun sid ->
                let dag = List.fold foldEvent (empty sid "") history
                Promise.lift dag)

        s.mergeBaseOverride <-
            Some(fun c a d ->
                s.mergeBaseIsAncestorCalls <- s.mergeBaseIsAncestorCalls @ [ (c, a, d) ]
                true)

        s.revParseRefOverride <-
            Some(fun c r ->
                s.revParseRefCalls <- s.revParseRefCalls @ [ (c, r) ]
                "merged-sha")

        rt.MasterSessionId <- sessionId
        do! replayFromEventLog rt

        match findTask "squad-a1b2" rt.Dag with
        | None -> checkBare false
        | Some t ->
            checkBare (t.Status = Merged)
            checkBare (t.MergedSha = Some "merged-sha")
    }

let testReplayWarnsOrphanRunningTasks () : JS.Promise<unit> =
    promise {
        let s = mkFake ()
        let deps = mkDeps s
        let rt = mkRuntime deps

        let sessionId = "squad-session-001"
        let evt1 = SquadCreated(sessionId, "req")

        let evt2 =
            TasksCreated(
                sessionId,
                [ { taskId = "squad-a1b2"
                    title = "A"
                    description = "desc"
                    dependsOn = [] } ]
            )

        let evt3 = TaskStarted(sessionId, "squad-a1b2", "/wt/a", "squad-a1b2")
        let history = [ evt1; evt2; evt3 ]

        s.getLatestSquadSessionIdOverride <- Some(fun () -> Promise.lift (Some sessionId))

        s.getSquadDagOverride <-
            Some(fun sid ->
                let dag = List.fold foldEvent (empty sid "") history
                Promise.lift dag)

        s.promptSessionOverride <-
            Some(fun c m p ->
                s.promptSessionCalls <- s.promptSessionCalls @ [ (m, p) ]
                s.orphanWarningSent <- true
                Promise.lift ())

        rt.MasterSessionId <- sessionId
        s.mergeBaseOverride <- Some(fun _ _ _ -> false)
        do! replayFromEventLog rt

        match findTask "squad-a1b2" rt.Dag with
        | None -> checkBare false
        | Some t -> checkBare (t.Status = Running)

        checkBare s.orphanWarningSent

        let callMsg =
            s.promptSessionCalls |> List.tryHead |> Option.map snd |> Option.defaultValue ""

        checkBare (callMsg.Contains "orphan" || callMsg.Contains "Orphan")
    }

let testReplayWarnsOrphanRetryAndIdempotency () : JS.Promise<unit> =
    promise {
        let s = mkFake ()
        let deps = mkDeps s
        let rt = mkRuntime deps

        let sessionId = "squad-session-001"
        let evt1 = SquadCreated(sessionId, "req")

        let evt2 =
            TasksCreated(
                sessionId,
                [ { taskId = "squad-a1b2"
                    title = "A"
                    description = "desc"
                    dependsOn = [] } ]
            )

        let evt3 = TaskStarted(sessionId, "squad-a1b2", "/wt/a", "squad-a1b2")
        let history = [ evt1; evt2; evt3 ]

        s.getLatestSquadSessionIdOverride <- Some(fun () -> Promise.lift (Some sessionId))

        s.getSquadDagOverride <-
            Some(fun sid ->
                let dag = List.fold foldEvent (empty sid "") history
                Promise.lift dag)

        let mutable promptCallCount = 0
        let mutable shouldFail = true

        s.promptSessionOverride <-
            Some(fun c m p ->
                promptCallCount <- promptCallCount + 1

                if shouldFail then
                    Promise.reject (System.Exception("intentional network error"))
                else
                    Promise.lift ())

        rt.MasterSessionId <- sessionId
        s.mergeBaseOverride <- Some(fun _ _ _ -> false)

        // 1st run: fails. Warning should NOT be recorded in SentWarnings
        do! replayFromEventLog rt
        equal 1 promptCallCount
        checkBare (rt.SentWarnings.Count = 0)

        // 2nd run: succeeds. Warning should be recorded in SentWarnings
        shouldFail <- false
        do! replayFromEventLog rt
        equal 2 promptCallCount
        checkBare (rt.SentWarnings.Count = 1)

        // 3rd run: already succeeded, should NOT call PromptSession again (deduped/idempotent)
        do! replayFromEventLog rt
        equal 2 promptCallCount
        checkBare (rt.SentWarnings.Count = 1)
    }

let testReplayWarnsOrphanEmptyMasterSessionId () : JS.Promise<unit> =
    promise {
        let s = mkFake ()
        let deps = mkDeps s
        let rt = mkRuntime deps

        let sessionId = "squad-session-001"
        let evt1 = SquadCreated(sessionId, "req")

        let evt2 =
            TasksCreated(
                sessionId,
                [ { taskId = "squad-a1b2"
                    title = "A"
                    description = "desc"
                    dependsOn = [] } ]
            )

        let evt3 = TaskStarted(sessionId, "squad-a1b2", "/wt/a", "squad-a1b2")
        let history = [ evt1; evt2; evt3 ]

        s.getLatestSquadSessionIdOverride <- Some(fun () -> Promise.lift (Some sessionId))

        s.getSquadDagOverride <-
            Some(fun sid ->
                let dag = List.fold foldEvent (empty sid "") history
                Promise.lift dag)

        // Empty MasterSessionId
        rt.MasterSessionId <- ""
        s.mergeBaseOverride <- Some(fun _ _ _ -> false)

        let mutable loggedDiagnostics: obj option = None
        // Temporarily override console.error
        emitJsStatement (fun msg -> loggedDiagnostics <- Some msg) "const oldErr = console.error; console.error = $0;"

        try
            do! replayFromEventLog rt
        finally
            emitJsStatement () "console.error = oldErr;"

        match loggedDiagnostics with
        | None -> checkBare false
        | Some diag ->
            let ev = Wanxiangshu.Runtime.Dyn.str diag "event"
            let msg = Wanxiangshu.Runtime.Dyn.str diag "message"
            let warning = Wanxiangshu.Runtime.Dyn.str diag "warning"

            equal "wanxiangzhen_orphan_tasks_diagnostic" ev
            checkBare (msg.Contains "MasterSessionId is empty")
            checkBare (warning.Contains "Orphan running tasks")
    }

let entriesAsync () : (string * (unit -> JS.Promise<unit>)) list =
    [ ("ExtendedMockE2e.chat_message_captures_session_id_and_replays", testChatMessageCapturesSessionIdAndReplays)

      ("ExtendedMockE2e.replay_reconciles_submitted_to_merged", testReplayReconcilesSubmittedToMerged)

      ("ExtendedMockE2e.replay_warns_orphan_running_tasks", testReplayWarnsOrphanRunningTasks)

      ("ExtendedMockE2e.replay_warns_orphan_retry_and_idempotency", testReplayWarnsOrphanRetryAndIdempotency)

      ("ExtendedMockE2e.replay_warns_orphan_empty_master_session_id", testReplayWarnsOrphanEmptyMasterSessionId) ]
