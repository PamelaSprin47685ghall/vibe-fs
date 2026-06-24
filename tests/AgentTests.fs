module VibeFs.Tests.AgentTests

open Fable.Core
open VibeFs.Tests.Assert
open VibeFs.Kernel.Config

let canUse' () =
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

    check "manager can knowledge_graph_fetch" (canUse "manager" "knowledge_graph_fetch")
    check "coder can knowledge_graph_fetch mirrors fuzzy_find" (canUse "coder" "knowledge_graph_fetch")
    check "reviewer denied knowledge_graph_fetch mirrors fuzzy_find" (not (canUse "reviewer" "knowledge_graph_fetch"))
    check "bookkeeper denied knowledge_graph_fetch mirrors fuzzy_find" (not (canUse "bookkeeper" "knowledge_graph_fetch"))
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
    check "investigator allowed todo" (canUse "investigator" "todowrite")

    check "coder can read" (canUse "coder" "read")
    check "coder can write" (canUse "coder" "write")
    check "coder can edit" (canUse "coder" "edit")
    check "coder can fuzzy_find" (canUse "coder" "fuzzy_find")
    check "coder can fuzzy_grep" (canUse "coder" "fuzzy_grep")
    check "coder denied investigator dispatch" (not (canUse "coder" "investigator"))
    check "coder allowed todo" (canUse "coder" "todowrite")

    check "meditator allowed todo" (canUse "meditator" "todowrite")

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
    check "unknown agent knowledge_graph_fetch mirrors fuzzy_find" (canUse "build" "knowledge_graph_fetch" = canUse "build" "fuzzy_find")

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
    let matrix : (string * (bool list)) list = [
        "knowledge_graph_fetch",                    [ true;  true;  true;  false; false; false; false; false ]
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
        "todowrite",                     [ true;  true;  true;  true;  true;  true;  true;  true  ]
        "todo_write",                    [ true;  true;  true;  true;  true;  true;  true;  true  ]
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
        "manage_todo_list",              [ true;  true;  true;  true;  true;  true;  true;  true  ]
    ]
    matrix
    |> List.iter (fun (tool, expected) ->
        let actual = agents |> List.map (fun agent -> canUse agent tool)
        check $"matrix: {tool}" (actual = expected))
