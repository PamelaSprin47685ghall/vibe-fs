module VibeFs.Tests.AgentTests

open VibeFs.Tests.Assert
open VibeFs.Kernel.AgentRole
open VibeFs.Kernel.Permission
open VibeFs.Kernel.AgentPolicy
open VibeFs.Kernel.HostKernel
open VibeFs.Kernel.MuxPolicy
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
    check "orchestrator fuzzy_find allowed" (List.contains "fuzzy_find" orchestrator.allowedTools)
    check "orchestrator fuzzy_find not in deniedTools" (not (List.contains "fuzzy_find" orchestrator.deniedTools))
    check "orchestrator fuzzy_grep denied" (List.contains "fuzzy_grep" orchestrator.deniedTools)
    check "orchestrator fuzzy_grep not allowed" (not (List.contains "fuzzy_grep" orchestrator.allowedTools))
    check "editor can write" (List.contains "write" (effectivePolicy Editor).allowedTools)
    check "reviewer submit_review_result" (List.contains "submit_review_result" (effectivePolicy Reviewer).allowedTools)
    check "greper fuzzy_grep" (List.contains "fuzzy_grep" (effectivePolicy Greper).allowedTools)
    equal "bash denied" (Some Deny) (Map.tryFind "bash" orchestrator.permissions)
    check "question denied for editor" (Map.tryFind "question" (effectivePolicy Editor).permissions = Some Deny)

let private toolPermissionLabel = function Allow -> "allow" | Deny -> "deny"

let private roleLabel (role: AgentRole) = VibeFs.Kernel.AgentRole.toString role

let private expectedAllowedDenied (role: AgentRole) =
    let tools = toolMapFor role
    canonicalToolNames
    |> List.choose (fun name -> Map.tryFind name tools |> Option.map (fun p -> name, p))
    |> List.partition (fun (_, p) -> p = Allow)
    |> fun (allowed, denied) -> List.map fst allowed, List.map fst denied

let effectivePolicyDeniedToolsCrossValidation () =
    allRoles
    |> List.iter (fun role ->
        let policy = effectivePolicy role
        let tools = toolMapFor role
        let permissions = defaultPermissions role
        let expectedAllowed, expectedDenied =
            expectedAllowedDenied role
        let expectedDeniedPermissions =
            permissions
            |> Map.toList
            |> List.choose (fun (n, p) -> if p = Deny then Some n else None)
        equal $"{roleLabel role} role" role policy.role
        equal $"{roleLabel role} tools" tools policy.tools
        equal $"{roleLabel role} permissions" permissions policy.permissions
        equal $"{roleLabel role} allowedTools" expectedAllowed policy.allowedTools
        equal $"{roleLabel role} deniedTools" expectedDenied policy.deniedTools
        equal $"{roleLabel role} deniedPermissions" expectedDeniedPermissions policy.deniedPermissions
        let union = Set.ofList (policy.allowedTools @ policy.deniedTools)
        check $"{roleLabel role} allowed+denied disjoint" (union.Count = policy.allowedTools.Length + policy.deniedTools.Length)
        check $"{roleLabel role} covers canonical tools" (union.Count = canonicalToolNames.Length))

let subagentToolPolicyUsesHostToolNames () =
  allRoles
  |> List.iter (fun role ->
    equal $"{roleLabel role} subagent disabledTools"
      (expandPatterns ((effectivePolicy role).deniedTools))
      (subagentToolPolicy role).disabledTools)

  check "orchestrator subagent disables apply_patch"
    (List.contains "apply_patch" (subagentToolPolicy Orchestrator).disabledTools)
  check "editor subagent keeps apply_patch"
    (not (List.contains "apply_patch" (subagentToolPolicy Editor).disabledTools))


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
