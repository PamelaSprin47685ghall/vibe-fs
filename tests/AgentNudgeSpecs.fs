module Wanxiangshu.Tests.AgentNudgeSpecs

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeDerivation

let private snap todos msg blocked agent isLoop : Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot =
    { todos = todos; lastAssistantMessage = msg; isLoopActive = isLoop
      nudgeBlockedForTurn = blocked; nudgeAnchorKey = msg; agentFromMessage = agent
      hasActiveRunner = false }

let private snap' todos msg blocked agent isLoop hasActiveRunner : Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot =
    { todos = todos; lastAssistantMessage = msg; isLoopActive = isLoop
      nudgeBlockedForTurn = blocked; nudgeAnchorKey = msg; agentFromMessage = agent
      hasActiveRunner = hasActiveRunner }

let decision () =
    equal "todos -> NudgeTodo" NudgeTodo (deriveAction (snap [ "a" ] "working" false None false))
    equal "todos+question -> None" NudgeNone (deriveAction (snap [ "a" ] "what now?" false None false))
    equal "todos+skip -> None" NudgeNone (deriveAction (snap [ "a" ] "done <skip-todo-check />" false None false))
    equal "nothing -> None" NudgeNone (deriveAction (snap [] "ok" false None false))
    equal "loop -> NudgeLoop" NudgeLoop (deriveAction (snap [] "ok" false None true))
    equal "loop+skip -> None" NudgeNone (deriveAction (snap [] "done <skip-loop-check />" false None true))
    equal "todos+activeRunner -> None"
        NudgeNone (deriveAction (snap' [ "a" ] "working" false None false true))

let dedupFromIntegral () =
    equal "blocked turn -> None" NudgeNone (deriveAction (snap [ "a" ] "working" true None false))
    equal "unblocked -> NudgeTodo" NudgeTodo (deriveAction (snap [ "a" ] "working" false None false))

let decideNudge' () =
    match deriveAction (snap [ "a" ] "working" false None false) with
    | NudgeTodo -> check "fresh turn nudges todo" true
    | _ -> check "fresh turn nudges todo" false

    match deriveAction (snap [ "a" ] "working" true None false) with
    | NudgeNone -> check "blocked turn -> StandDown" true
    | _ -> check "blocked turn -> StandDown" false

    match deriveAction (snap [] "done" false None false) with
    | NudgeNone -> check "no work -> StandDown" true
    | _ -> check "no work -> StandDown" false

    match deriveAction (snap [] "ok" false None true) with
    | NudgeLoop -> check "loop active nudges loop" true
    | _ -> check "loop active nudges loop" false

let selectPrompt () =
    let todoSnapshot = snap [ "todo1"; "todo2" ] "working" false None false
    let loopSnapshot = snap [] "ok" false None true
    let noneSnapshot = snap [] "done" false None false

    match selectNudgePrompt NudgeTodo todoSnapshot with
    | Some prompt ->
        check "selectNudgePrompt NudgeTodo returns prompt" true
        check "todo prompt contains front matter" (prompt.Contains("---"))
        check "todo prompt contains todos" (prompt.Contains("todos"))
        check "todo prompt contains todo content" (prompt.Contains("todo1"))
    | None -> check "selectNudgePrompt NudgeTodo returns prompt" false

    match selectNudgePrompt NudgeLoop loopSnapshot with
    | Some prompt ->
        check "selectNudgePrompt NudgeLoop returns prompt" true
        check "loop prompt contains front matter" (prompt.Contains("---"))
    | None -> check "selectNudgePrompt NudgeLoop returns prompt" false

    match selectNudgePrompt NudgeNone noneSnapshot with
    | None -> check "selectNudgePrompt NudgeNone returns None" true
    | Some _ -> check "selectNudgePrompt NudgeNone returns None" false

let run () =
    decision ()
    dedupFromIntegral ()
    decideNudge' ()
    selectPrompt ()