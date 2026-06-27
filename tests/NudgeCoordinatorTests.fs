module Wanxiangshu.Tests.NudgeCoordinatorTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Coordinator

// ── helpers ────────────────────────────────────────────────────────────────

let private ctxEmpty : NudgeContext =
    { todos = []; lastAssistantMessage = ""; hasActiveRunner = false; isLoopActive = false }

let private ctxTodo (msg: string) : NudgeContext =
    { todos = [ "t1" ]; lastAssistantMessage = msg; hasActiveRunner = false; isLoopActive = false }

let private ctxRunner (msg: string) : NudgeContext =
    { todos = []; lastAssistantMessage = msg; hasActiveRunner = true; isLoopActive = false }

let private ctxLoop (msg: string) : NudgeContext =
    { todos = []; lastAssistantMessage = msg; hasActiveRunner = false; isLoopActive = true }

let private actStr (action: NudgeAction) : string = toString action

// ── freshSession / freshCoordinator / freshCoordinatorRuntime ───────────────

let freshSessionTest () =
    let s = freshSession
    equal "lastAction = None" None s.lastAction
    equal "lastMessage = empty" "" s.lastMessage

let freshCoordinatorTest () =
    let c = freshCoordinator
    check "sessions = Map.empty" (Map.isEmpty c.sessions)

let freshCoordinatorRuntimeTest () =
    let r = freshCoordinatorRuntime
    equal "coordinator = freshCoordinator" freshCoordinator r.coordinator
    check "suppressedSessions = Set.empty" (Set.isEmpty r.suppressedSessions)

// ── update ─────────────────────────────────────────────────────────────────

let updateNudgeNoneTest () =
    let state = freshCoordinator
    let next, action = update state "s1" ctxEmpty
    equal "action = NudgeNone" NudgeNone action
    check "state unchanged" (next = state)

let updateSameDedupTest () =
    let state = freshCoordinator
    let s1, a1 = update state "s1" (ctxTodo "same msg")
    let state2, a2 = update s1 "s1" (ctxTodo "same msg")
    equal "first call → NudgeTodo" NudgeTodo a1
    equal "second call dedup → NudgeNone" NudgeNone a2

let updateDifferentTest () =
    let state = freshCoordinator
    // first: todos → NudgeTodo
    let s1, a1 = update state "s1" (ctxTodo "hello")
    equal "todo action" NudgeTodo a1
    // second: runner → NudgeRunner  (different action, even with same message)
    let s2, a2 = update s1 "s1" (ctxRunner "hello")
    equal "runner action" NudgeRunner a2

let updateNewSessionTest () =
    let state = freshCoordinator
    let s1, action = update state "s1" (ctxTodo "first")
    equal "new session → NudgeTodo" NudgeTodo action
    check "session stored in map" (Map.containsKey "s1" s1.sessions)
    equal "stored lastAction" (Some NudgeTodo) (Map.find "s1" s1.sessions).lastAction
    equal "stored lastMessage" "first" (Map.find "s1" s1.sessions).lastMessage

// ── shouldSuppressNudge ─────────────────────────────────────────────────────

let shouldSuppressNudgeQuestionTest () =
    let ctx = { ctxEmpty with lastAssistantMessage = "what now?" }
    check "question → suppress" (shouldSuppressNudge "s" ctx None)

let shouldSuppressNudgeSkipTodoTest () =
    let ctx = { ctxEmpty with lastAssistantMessage = "done <skip-todo-check/>" }
    check "skip-todo → suppress" (shouldSuppressNudge "s" ctx None)

let shouldSuppressNudgeSkipLoopTest () =
    let ctx = { ctxEmpty with lastAssistantMessage = "done <skip-loop-check/>" }
    check "skip-loop → suppress" (shouldSuppressNudge "s" ctx None)

let shouldSuppressNudgePreviousSameTest () =
    // previousAction matches what decide would return (todos → NudgeTodo)
    let ctx = { todos = [ "a" ]; lastAssistantMessage = "working"; hasActiveRunner = false; isLoopActive = false }
    check "same previous action → suppress" (shouldSuppressNudge "s" ctx (Some NudgeTodo))

let shouldSuppressNudgePreviousDifferentTest () =
    // previous=NudgeTodo but context triggers NudgeRunner
    let ctx = { todos = []; lastAssistantMessage = "ok"; hasActiveRunner = true; isLoopActive = false }
    check "different previous action → no suppress" (not (shouldSuppressNudge "s" ctx (Some NudgeTodo)))

let shouldSuppressNudgeNoPreviousTest () =
    let ctx = { todos = [ "a" ]; lastAssistantMessage = "working"; hasActiveRunner = false; isLoopActive = false }
    check "no previous → no suppress" (not (shouldSuppressNudge "s" ctx None))

// ── consumeSuppression ──────────────────────────────────────────────────────

let consumeSuppressionPresentTest () =
    let state = { freshCoordinatorRuntime with suppressedSessions = set [ "s1" ] }
    let next, removed = consumeSuppression state "s1"
    check "removed = true" removed
    check "session removed from set" (not (Set.contains "s1" next.suppressedSessions))

let consumeSuppressionAbsentTest () =
    let state = { freshCoordinatorRuntime with suppressedSessions = set [ "other" ] }
    let next, removed = consumeSuppression state "s1"
    check "removed = false" (not removed)
    check "state unchanged" (next = state)

// ── suppressSession ─────────────────────────────────────────────────────────

let suppressSessionTest () =
    let state = { freshCoordinatorRuntime with suppressedSessions = set [ "x" ] }
    let next = suppressSession state "s1"
    check "new session added" (Set.contains "s1" next.suppressedSessions)
    check "old session still present" (Set.contains "x" next.suppressedSessions)

// ── clearRuntimeSession ─────────────────────────────────────────────────────

let clearRuntimeSessionTest () =
    let coord = { freshCoordinator with sessions = Map.add "s1" freshSession Map.empty }
    let state = { coordinator = coord; suppressedSessions = set [ "s1"; "other" ] }
    let next = clearRuntimeSession state "s1"
    check "session removed from coordinator" (not (Map.containsKey "s1" next.coordinator.sessions))
    check "session removed from suppressed" (not (Set.contains "s1" next.suppressedSessions))
    check "other session still suppressed" (Set.contains "other" next.suppressedSessions)

// ── decideRuntimeAction ─────────────────────────────────────────────────────

let decideRuntimeActionSuppressedTest () =
    let state = suppressSession freshCoordinatorRuntime "s1"
    let next, action = decideRuntimeAction state "s1" (ctxTodo "msg")
    equal "suppressed → none" "none" action
    // consumption means second call is no longer suppressed
    let next2, action2 = decideRuntimeAction next "s1" (ctxTodo "msg")
    equal "after consumption → nudge-todo" "nudge-todo" action2

let decideRuntimeActionNormalTest () =
    let state = freshCoordinatorRuntime
    // ctxTodo → NudgeTodo
    let _, a1 = decideRuntimeAction state "s1" (ctxTodo "hi")
    equal "todo context → nudge-todo" "nudge-todo" a1
    // ctxRunner → NudgeRunner
    let _, a2 = decideRuntimeAction state "s1" (ctxRunner "hi")
    equal "runner context → nudge-runner" "nudge-runner" a2
    // ctxLoop → NudgeLoop
    let _, a3 = decideRuntimeAction state "s1" (ctxLoop "hi")
    equal "loop context → nudge-loop" "nudge-loop" a3
    // ctxEmpty → NudgeNone
    let _, a4 = decideRuntimeAction state "s1" ctxEmpty
    equal "empty context → none" "none" a4

// ── run ─────────────────────────────────────────────────────────────────────

let run () : unit =
    // fresh*
    freshSessionTest ()
    freshCoordinatorTest ()
    freshCoordinatorRuntimeTest ()
    // update
    updateNudgeNoneTest ()
    updateSameDedupTest ()
    updateDifferentTest ()
    updateNewSessionTest ()
    // shouldSuppressNudge
    shouldSuppressNudgeQuestionTest ()
    shouldSuppressNudgeSkipTodoTest ()
    shouldSuppressNudgeSkipLoopTest ()
    shouldSuppressNudgePreviousSameTest ()
    shouldSuppressNudgePreviousDifferentTest ()
    shouldSuppressNudgeNoPreviousTest ()
    // consumeSuppression
    consumeSuppressionPresentTest ()
    consumeSuppressionAbsentTest ()
    // suppressSession
    suppressSessionTest ()
    // clearRuntimeSession
    clearRuntimeSessionTest ()
    // decideRuntimeAction
    decideRuntimeActionSuppressedTest ()
    decideRuntimeActionNormalTest ()
