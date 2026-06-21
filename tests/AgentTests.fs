module VibeFs.Tests.AgentTests

open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Config
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.NudgeState

let canUse' () =
    check "agent_report for manager" (canUse "manager" "agent_report")
    check "agent_report for investigator" (canUse "investigator" "agent_report")
    check "agent_report for coder" (canUse "coder" "agent_report")

    check "bash denied for manager" (not (canUse "manager" "bash"))
    check "bash_.* denied for coder" (not (canUse "coder" "bash_run"))
    check "task denied for investigator" (not (canUse "investigator" "task"))
    check "grep denied exact" (not (canUse "manager" "grep"))
    check "fuzzy_grep not caught by grep rule" (canUse "investigator" "fuzzy_grep")

    check "stealth for browser" (canUse "browser" "stealth-browser-mcp_navigate")
    check "stealth denied for manager" (not (canUse "manager" "stealth-browser-mcp_navigate"))

    check "return_reviewer for reviewer" (canUse "reviewer" "return_reviewer")
    check "return_reviewer denied for manager" (not (canUse "manager" "return_reviewer"))
    check "submit_review for manager" (canUse "manager" "submit_review")
    check "submit_review denied for coder" (not (canUse "coder" "submit_review"))
    check "submit_review denied for investigator" (not (canUse "investigator" "submit_review"))

    check "meditator denied read" (not (canUse "meditator" "read"))
    check "executor denied read" (not (canUse "executor" "read"))
    check "meditator agent_report ok" (canUse "meditator" "agent_report")

    check "reviewer can read" (canUse "reviewer" "read")
    check "reviewer denied coder" (not (canUse "reviewer" "coder"))
    check "reviewer denied fuzzy_find" (not (canUse "reviewer" "fuzzy_find"))

    check "manager can fetch_wiki" (canUse "manager" "fetch_wiki")
    check "coder can fetch_wiki mirrors fuzzy_find" (canUse "coder" "fetch_wiki")
    check "reviewer denied fetch_wiki mirrors fuzzy_find" (not (canUse "reviewer" "fetch_wiki"))
    check "bookkeeper denied fetch_wiki mirrors fuzzy_find" (not (canUse "bookkeeper" "fetch_wiki"))
    check "bookkeeper can return_bookkeeper" (canUse "bookkeeper" "return_bookkeeper")
    check "manager denied return_bookkeeper" (not (canUse "manager" "return_bookkeeper"))
    check "bookkeeper denied read" (not (canUse "bookkeeper" "read"))
    check "bookkeeper denied coder dispatch" (not (canUse "bookkeeper" "coder"))
    check "bookkeeper denied websearch" (not (canUse "bookkeeper" "websearch"))

    check "browser can read" (canUse "browser" "read")
    check "browser denied coder" (not (canUse "browser" "coder"))

    check "investigator can read" (canUse "investigator" "read")
    check "investigator can executor" (canUse "investigator" "executor")
    check "investigator can fuzzy_find" (canUse "investigator" "fuzzy_find")
    check "investigator can fuzzy_grep" (canUse "investigator" "fuzzy_grep")
    check "investigator denied write" (not (canUse "investigator" "write"))
    check "investigator denied coder dispatch" (not (canUse "investigator" "coder"))
    check "investigator denied todo" (not (canUse "investigator" "todowrite"))

    check "coder can read" (canUse "coder" "read")
    check "coder can write" (canUse "coder" "write")
    check "coder can edit" (canUse "coder" "edit")
    check "coder can fuzzy_find" (canUse "coder" "fuzzy_find")
    check "coder can fuzzy_grep" (canUse "coder" "fuzzy_grep")
    check "coder denied investigator dispatch" (not (canUse "coder" "investigator"))
    check "coder denied todo" (not (canUse "coder" "todowrite"))

    check "manager can read" (canUse "manager" "read")
    check "manager can coder dispatch" (canUse "manager" "coder")
    check "manager can investigator dispatch" (canUse "manager" "investigator")
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
    check "unknown agent can investigator dispatch" (canUse "build" "investigator")
    check "unknown agent can fuzzy_find" (canUse "build" "fuzzy_find")
    check "unknown agent fetch_wiki mirrors fuzzy_find" (canUse "build" "fetch_wiki" = canUse "build" "fuzzy_find")

let deniedTools' () =
    let tools = [ "coder"; "investigator"; "read"; "write"; "bash"; "fuzzy_find"; "fuzzy_grep"; "agent_report" ]
    let denied = deniedTools "investigator" tools |> Set.ofList
    check "investigator denied write" (Set.contains "write" denied)
    check "investigator denied bash" (Set.contains "bash" denied)
    check "investigator denied coder dispatch" (Set.contains "coder" denied)
    check "investigator keeps read" (not (Set.contains "read" denied))
    check "investigator keeps fuzzy_find" (not (Set.contains "fuzzy_find" denied))
    check "investigator keeps fuzzy_grep" (not (Set.contains "fuzzy_grep" denied))
    check "investigator keeps agent_report" (not (Set.contains "agent_report" denied))

/// Full permission characterization: snapshot `canUse agent tool` for every
/// known agent across a tool set that exercises each permission rule (reserved
/// tools, agent_report, shell/grep, stealth, return_<role>, read, terminal
/// roles, dispatch/planning/web/todo/skill, mutating file tools, fuzzy_grep).
/// Order matters — this is the lock that lets Config.canUseCanonical be
/// reshaped without drifting behavior (REFACTOR.md §1 D8).
let canUseMatrix () =
    let agents =
        [ "manager"; "investigator"; "coder"; "reviewer"; "browser"; "meditator"; "executor"; "bookkeeper" ]
    // (tool, expected-allow per agent in `agents` order)
    let matrix : (string * (bool list)) list = [
        "fetch_wiki",                    [ true;  true;  true;  false; false; false; false; false ]
        "return_bookkeeper",             [ false; false; false; false; false; false; false; true  ]
        "agent_report",                  [ true;  true;  true;  true;  true;  true;  true;  true  ]
        "bash",                          [ false; false; false; false; false; false; false; false ]
        "bash_run",                      [ false; false; false; false; false; false; false; false ]
        "task",                          [ false; false; false; false; false; false; false; false ]
        "grep",                          [ false; false; false; false; false; false; false; false ]
        "grep_x",                        [ true;  true;  true;  false; false; false; false; false ]
        "fuzzy_grep",                    [ false; true;  true;  false; false; false; false; false ]
        "fuzzy_find",                    [ true;  true;  true;  false; false; false; false; false ]
        "glob",                          [ true;  true;  true;  false; false; false; false; false ]
        "read",                          [ true;  true;  true;  true;  true;  false; false; false ]
        "write",                         [ false; false; true;  false; false; false; false; false ]
        "edit",                          [ false; false; true;  false; false; false; false; false ]
        "patch",                         [ false; false; true;  false; false; false; false; false ]
        "ast_edit",                      [ false; false; true;  false; false; false; false; false ]
        "apply_patch",                   [ false; false; true;  false; false; false; false; false ]
        "websearch",                     [ true;  false; false; false; false; false; false; false ]
        "webfetch",                      [ true;  false; false; false; false; false; false; false ]
        "submit_review",                 [ true;  false; false; false; false; false; false; false ]
        "todowrite",                     [ true;  false; false; false; false; false; false; false ]
        "todo_write",                    [ true;  false; false; false; false; false; false; false ]
        "question",                      [ true;  false; false; false; false; false; false; false ]
        "ask_user_question",             [ true;  false; false; false; false; false; false; false ]
        "skill",                         [ true;  false; false; false; false; false; false; false ]
        "coder",                         [ true;  false; false; false; false; false; false; false ]
        "investigator",                  [ true;  false; false; false; false; false; false; false ]
        "meditator",                     [ true;  false; false; false; false; false; false; false ]
        "browser",                       [ true;  false; false; false; false; false; false; false ]
        "manager",                       [ true;  false; false; false; false; false; false; false ]
        "executor",                      [ true;  true;  false; false; false; false; false; false ]
        "stealth-browser-mcp_navigate",  [ false; false; false; false; true;  false; false; false ]
        "return_reviewer",               [ false; false; false; true;  false; false; false; false ]
        "return_coder",                  [ false; false; true;  false; false; false; false; false ]
        "manage_todo_list",              [ true;  false; false; false; false; false; false; false ]
    ]
    matrix
    |> List.iter (fun (tool, expected) ->
        let actual = agents |> List.map (fun agent -> canUse agent tool)
        check $"matrix: {tool}" (actual = expected))

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

let coordinatorRuntime () =
    let mutable coord = freshCoordinatorRuntime
    let ctx : NudgeContext = { todos = [ "a" ]; lastAssistantMessage = "working"; hasActiveRunner = false; isLoopActive = false }
    let next1, action1 = decideRuntimeAction coord "s" ctx
    coord <- next1
    equal "first nudge todo" "nudge-todo" action1
    let next2, action2 = decideRuntimeAction coord "s" ctx
    coord <- next2
    equal "same message suppressed" "none" action2
    let ctxNew = { ctx with lastAssistantMessage = "new output" }
    let next3, action3 = decideRuntimeAction coord "s" ctxNew
    coord <- next3
    equal "new message re-nudge" "nudge-todo" action3
    coord <- suppressSession coord "s"
    let next4, action4 = decideRuntimeAction coord "s" ctxNew
    coord <- next4
    equal "explicit suppress none" "none" action4
    coord <- clearRuntimeSession coord "s"
    let _, action5 = decideRuntimeAction coord "s" ctx
    equal "after clear todo" "nudge-todo" action5

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

let private snapshot todos msg alreadyNudged agent : SessionSnapshot =
    { todos = todos; lastAssistantMessage = msg; alreadyNudged = alreadyNudged; agentFromMessage = agent }

let private noReview (_: string) = false
let private noChild (_: string) = None

/// decideNudge is the restart-safe nudge gate. The per-stop de-dup signal is
/// `alreadyNudged`, read from the dialogue history, NOT an in-memory counter —
/// so a restart that wipes the counter can never resurrect a duplicate nudge.
let decideNudge' () =
    // Unclaimed session: nothing to do.
    let _, d0 = decideNudge noReview noChild emptyState "s" (snapshot [ "a" ] "working" false None)
    equal "unclaimed → StandDown" StandDown d0

    // Claimed + open todos + fresh stop → Send the todo nudge.
    let claimed, _ = tryClaimNudge emptyState "s"
    match snd (decideNudge noReview noChild claimed "s" (snapshot [ "a" ] "working" false None)) with
    | Send(text, _) -> check "claimed fresh stop nudges todo" (text = VibeFs.Kernel.Prompts.todoNudgePrompt)
    | StandDown -> check "claimed fresh stop nudges todo" false

    // Claimed but the history already carries a trailing nudge → stand down.
    let _, dDup = decideNudge noReview noChild claimed "s" (snapshot [ "a" ] "working" true None)
    equal "already-nudged stop → StandDown" StandDown dDup

    // Claimed, no open todos, no loop → stand down.
    let _, dNone = decideNudge noReview noChild claimed "s" (snapshot [] "done" false None)
    equal "no work → StandDown" StandDown dNone

    // Claimed + loop active (no todos) → loop nudge.
    let loopReview (_: string) = true
    match snd (decideNudge loopReview noChild claimed "s" (snapshot [] "ok" false None)) with
    | Send(text, _) -> check "loop active nudges loop" (text = VibeFs.Kernel.Prompts.loopNudgePrompt)
    | StandDown -> check "loop active nudges loop" false

    // Stopped session is never nudged even when claimed.
    let stopped = stopSession claimed "s"
    let _, dStop = decideNudge noReview noChild stopped "s" (snapshot [ "a" ] "working" false None)
    equal "stopped → StandDown" StandDown dStop

/// decodeLastAssistant reads (text, agent, alreadyNudged) from the host message
/// array.  `alreadyNudged` is true iff a nudge-prompt user message trails the
/// last completed assistant turn — the durable, restart-proof de-dup anchor.
let decodeLastAssistantNudge () =
    let assistant text =
        box {| info = box {| role = "assistant"; finish = "stop" |}
               parts = [| box {| ``type`` = "text"; text = text |} |] |}
    let user text =
        box {| info = box {| role = "user" |}
               parts = [| box {| ``type`` = "text"; text = text |} |] |}

    let text1, agent1, nudged1 = decodeLastAssistant (box [| user "go"; assistant "did work" |])
    equal "last assistant text" "did work" text1
    equal "no agent field → None" None agent1
    check "no trailing nudge → false" (not nudged1)

    // A nudge prompt after the last assistant turn marks the stop as nudged.
    let _, _, nudged2 =
        decodeLastAssistant (box [| user "go"; assistant "did work"; user VibeFs.Kernel.Prompts.todoNudgePrompt |])
    check "trailing todo nudge → true" nudged2

    // Once the agent answers the nudge, a NEW assistant turn is the last one and
    // the stop is open for nudging again.
    let _, _, nudged3 =
        decodeLastAssistant (box [| user "go"; assistant "did work"; user VibeFs.Kernel.Prompts.todoNudgePrompt; assistant "more work" |])
    check "assistant after nudge → false" (not nudged3)

    let _, _, nudgedEmpty = decodeLastAssistant (box [||])
    check "empty history → false" (not nudgedEmpty)
