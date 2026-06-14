module VibeFs.Tests.AgentTests

open VibeFs.Tests.Assert
open VibeFs.Kernel.AgentRole
open VibeFs.Kernel.Permission
open VibeFs.Kernel.AgentPolicy
open VibeFs.Kernel.Nudge

let role () =
    let parse = VibeFs.Kernel.AgentRole.ofString
    let show = VibeFs.Kernel.AgentRole.toString
    equal "parse orchestrator" (Ok Orchestrator) (parse "orchestrator")
    equal "parse editor" (Ok Editor) (parse "editor")
    check "invalid role errors" (parse "nope" |> Result.isError)
    allRoles |> List.iter (fun r -> equal "role roundtrip" r (show r |> parse |> Result.defaultValue r))

let policy () =
    let orchestrator = effectivePolicy Orchestrator
    check "orchestrator can read" (List.contains "read" orchestrator.allowedTools)
    check "orchestrator can todowrite" (List.contains "todowrite" orchestrator.allowedTools)
    check "editor can write" (List.contains "write" (effectivePolicy Editor).allowedTools)
    check "reviewer submit_review_result" (List.contains "submit_review_result" (effectivePolicy Reviewer).allowedTools)
    check "greper fuzzy_grep" (List.contains "fuzzy_grep" (effectivePolicy Greper).allowedTools)
    equal "bash denied" (Some Deny) (Map.tryFind "bash" orchestrator.permissions)
    check "question denied for editor" (Map.tryFind "question" (effectivePolicy Editor).permissions = Some Deny)

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
    equal "repeat suppressed → None" NudgeNone action2

let coordinator () =
    let coord = NudgeCoordinator()
    let ctx : NudgeContext = { todos = [ "a" ]; lastAssistantMessage = "working"; hasActiveRunner = false; isLoopActive = false }
    equal "first nudge todo" "nudge-todo" (coord.shouldNudge ("s", ctx))
    equal "repeat suppressed" "none" (coord.shouldNudge ("s", ctx))
    coord.suppress "s"
    equal "explicit suppress none" "none" (coord.shouldNudge ("s", ctx))
    coord.clearSession "s"
    equal "after clear todo" "nudge-todo" (coord.shouldNudge ("s", ctx))
