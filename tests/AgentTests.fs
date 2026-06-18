module VibeFs.Tests.AgentTests

open VibeFs.Tests.Assert
open VibeFs.Kernel.ToolPolicy
open VibeFs.Kernel.Nudge
open VibeFs.Shell.NudgeStore

let canUse' () =
    check "agent_report for manager" (canUse "manager" "agent_report")
    check "agent_report for reader" (canUse "reader" "agent_report")
    check "agent_report for coder" (canUse "coder" "agent_report")

    check "bash denied for manager" (not (canUse "manager" "bash"))
    check "bash_.* denied for coder" (not (canUse "coder" "bash_run"))
    check "task denied for reader" (not (canUse "reader" "task"))
    check "grep denied exact" (not (canUse "manager" "grep"))
    check "fuzzy_grep not caught by grep rule" (canUse "reader" "fuzzy_grep")

    check "stealth for browser" (canUse "browser" "stealth-browser-mcp_navigate")
    check "stealth denied for manager" (not (canUse "manager" "stealth-browser-mcp_navigate"))

    check "return_reviewer for reviewer" (canUse "reviewer" "return_reviewer")
    check "return_reviewer denied for manager" (not (canUse "manager" "return_reviewer"))
    check "submit_review for manager" (canUse "manager" "submit_review")
    check "submit_review denied for coder" (not (canUse "coder" "submit_review"))
    check "submit_review denied for reader" (not (canUse "reader" "submit_review"))

    check "meditator denied read" (not (canUse "meditator" "read"))
    check "executor denied read" (not (canUse "executor" "read"))
    check "meditator agent_report ok" (canUse "meditator" "agent_report")

    check "reviewer can read" (canUse "reviewer" "read")
    check "reviewer denied coder" (not (canUse "reviewer" "coder"))
    check "reviewer denied fuzzy_find" (not (canUse "reviewer" "fuzzy_find"))

    check "browser can read" (canUse "browser" "read")
    check "browser denied coder" (not (canUse "browser" "coder"))

    check "reader can read" (canUse "reader" "read")
    check "reader can executor" (canUse "reader" "executor")
    check "reader can fuzzy_find" (canUse "reader" "fuzzy_find")
    check "reader can fuzzy_grep" (canUse "reader" "fuzzy_grep")
    check "reader denied write" (not (canUse "reader" "write"))
    check "reader denied coder dispatch" (not (canUse "reader" "coder"))
    check "reader denied todo" (not (canUse "reader" "todowrite"))

    check "coder can read" (canUse "coder" "read")
    check "coder can write" (canUse "coder" "write")
    check "coder can edit" (canUse "coder" "edit")
    check "coder can fuzzy_find" (canUse "coder" "fuzzy_find")
    check "coder can fuzzy_grep" (canUse "coder" "fuzzy_grep")
    check "coder denied reader dispatch" (not (canUse "coder" "reader"))
    check "coder denied todo" (not (canUse "coder" "todowrite"))

    check "manager can read" (canUse "manager" "read")
    check "manager can coder dispatch" (canUse "manager" "coder")
    check "manager can reader dispatch" (canUse "manager" "reader")
    check "manager can meditator dispatch" (canUse "manager" "meditator")
    check "manager can manage_todo_list" (canUse "manager" "manage_todo_list")
    check "manager allowed todowrite" (canUse "manager" "todowrite")
    check "manager can fuzzy_find" (canUse "manager" "fuzzy_find")
    check "manager denied fuzzy_grep" (not (canUse "manager" "fuzzy_grep"))
    check "manager denied write" (not (canUse "manager" "write"))
    check "manager denied edit" (not (canUse "manager" "edit"))

    check "unknown agent can read" (canUse "build" "read")
    check "unknown agent can write" (canUse "build" "write")
    check "unknown agent can coder dispatch" (canUse "build" "coder")
    check "unknown agent can reader dispatch" (canUse "build" "reader")

let deniedTools' () =
    let tools = [ "coder"; "reader"; "read"; "write"; "bash"; "fuzzy_find"; "fuzzy_grep"; "agent_report" ]
    let denied = deniedTools "reader" tools |> Set.ofList
    check "reader denied write" (Set.contains "write" denied)
    check "reader denied bash" (Set.contains "bash" denied)
    check "reader denied coder dispatch" (Set.contains "coder" denied)
    check "reader keeps read" (not (Set.contains "read" denied))
    check "reader keeps fuzzy_find" (not (Set.contains "fuzzy_find" denied))
    check "reader keeps fuzzy_grep" (not (Set.contains "fuzzy_grep" denied))
    check "reader keeps agent_report" (not (Set.contains "agent_report" denied))

let private nudgeContext todos msg runner loopActive =
    { todos = todos; lastAssistantMessage = msg; hasActiveRunner = runner; isLoopActive = loopActive }

let decision () =
    equal "todos → NudgeTodo" NudgeTodo (decide (nudgeContext [ "a" ] "working" false false))
    equal "todos+question → None" NudgeNone (decide (nudgeContext [ "a" ] "what now?" false false))
    equal "todos+skip → None" NudgeNone (decide (nudgeContext [ "a" ] "done <skip-todo-check />" false false))
    equal "runner → NudgeRunner" NudgeRunner (decide (nudgeContext [] "ok" true false))
    equal "loop → NudgeLoop" NudgeLoop (decide (nudgeContext [] "ok" false true))
    equal "loop+skip → None" NudgeNone (decide (nudgeContext [] "done <skip-loop-check />" false true))
    equal "nothing → None" NudgeNone (decide (nudgeContext [] "ok" false false))

let updateState () =
    let ctx = nudgeContext [ "a" ] "working" false false
    let state, action = update freshCoordinator "sess" ctx
    equal "update → NudgeTodo" NudgeTodo action
    check "state records session" (Map.containsKey "sess" state.sessions)
    let _, action2 = update state "sess" ctx
    equal "same message suppressed → None" NudgeNone action2
    let ctxNew = nudgeContext [ "a" ] "did more work" false false
    let _, action3 = update state "sess" ctxNew
    equal "new message allowed → NudgeTodo" NudgeTodo action3

let coordinator () =
    let coord = NudgeCoordinator()
    let ctx : NudgeContext = { todos = [ "a" ]; lastAssistantMessage = "working"; hasActiveRunner = false; isLoopActive = false }
    equal "first nudge todo" "nudge-todo" (coord.shouldNudge ("s", ctx))
    equal "same message suppressed" "none" (coord.shouldNudge ("s", ctx))
    let ctxNew = { ctx with lastAssistantMessage = "new output" }
    equal "new message re-nudge" "nudge-todo" (coord.shouldNudge ("s", ctxNew))
    coord.suppress "s"
    equal "explicit suppress none" "none" (coord.shouldNudge ("s", ctxNew))
    coord.clearSession "s"
    equal "after clear todo" "nudge-todo" (coord.shouldNudge ("s", ctx))

let shouldSuppress' () =
    let previous = Some NudgeTodo
    let repeated : NudgeContext =
        { todos = [ "a" ]; lastAssistantMessage = "did more work"
          hasActiveRunner = false; isLoopActive = false }
    let cleared : NudgeContext =
        { todos = []; lastAssistantMessage = "all done"
          hasActiveRunner = false; isLoopActive = false }
    let reopened : NudgeContext =
        { todos = [ "a" ]; lastAssistantMessage = "new open todos"
          hasActiveRunner = false; isLoopActive = false }

    check "same action suppressed across consecutive stream-end" (shouldSuppressNudge "s" repeated previous)
    check "cleared context resets suppression" (not (shouldSuppressNudge "s" cleared previous))
    check "reopened context re-allows todo nudge" (not (shouldSuppressNudge "s" reopened None))
