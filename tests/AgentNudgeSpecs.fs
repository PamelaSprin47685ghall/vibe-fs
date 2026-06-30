module Wanxiangshu.Tests.AgentNudgeSpecs

open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Coordinator
open Wanxiangshu.Kernel.NudgeState
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Shell.OpencodeSessionEventCodec


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
    { todos = todos; lastAssistantMessage = msg; isLoopActive = false; alreadyNudged = alreadyNudged; agentFromMessage = agent; lastAssistantIsCompaction = false; anchorPromptIssued = false }

let private noReview (_: string) = false
let private noChild (_: string) = None

/// decideNudge is the restart-safe nudge gate. The per-stop de-dup signal is
/// `alreadyNudged`, read from the dialogue history, NOT an in-memory counter —
/// so a restart that wipes the counter can never resurrect a duplicate nudge.
let decideNudge' () =
    match snd (decideNudge noChild emptyState "s" (snapshot [ "a" ] "working" false None)) with
    | Send(text, _) -> check "unclaimed fresh history nudges todo" (text = Wanxiangshu.Kernel.PromptFragments.todoNudgePrompt)
    | StandDown -> check "unclaimed fresh history nudges todo" false

    let claimed, _ = tryClaimNudge emptyState "s"
    match snd (decideNudge noChild claimed "s" (snapshot [ "a" ] "working" false None)) with
    | Send(text, _) -> check "claimed fresh stop nudges todo" (text = Wanxiangshu.Kernel.PromptFragments.todoNudgePrompt)
    | StandDown -> check "claimed fresh stop nudges todo" false

    let _, dDup = decideNudge noChild claimed "s" (snapshot [ "a" ] "working" true None)
    equal "already-nudged stop → StandDown" StandDown dDup

    let _, dNone = decideNudge noChild claimed "s" (snapshot [] "done" false None)
    equal "no work → StandDown" StandDown dNone

    let loopSnap = { snapshot [] "ok" false None with isLoopActive = true }
    match snd (decideNudge noChild claimed "s" loopSnap) with
    | Send(text, _) -> check "loop active nudges loop" (text = Wanxiangshu.Kernel.PromptFragments.loopNudgePrompt)
    | StandDown -> check "loop active nudges loop" false

    let lookupReviewer (_: string) = Some "reviewer"
    let claimedReviewer, _ = tryClaimNudge emptyState "reviewer-child-sess"
    match snd (decideNudge lookupReviewer claimedReviewer "reviewer-child-sess" { loopSnap with isLoopActive = true }) with
    | Send(text, _) ->
        check "reviewer child session must not get worker loopNudgePrompt" (text <> Wanxiangshu.Kernel.PromptFragments.loopNudgePrompt)
    | StandDown -> ()

    let stopped = stopSession claimed "s"
    let _, dStop = decideNudge noChild stopped "s" (snapshot [ "a" ] "working" false None)
    equal "stopped → StandDown" StandDown dStop

/// decodeLastAssistant reads (text, agent, alreadyNudged) from the host message
/// array.  `alreadyNudged` is true iff a nudge-prompt user message trails the
/// last completed assistant turn — the durable, restart-proof de-dup anchor.
let decodeLastAssistantNudge () =
    let assistant text =
        box {| info = box {| role = "assistant"; finish = "stop" |}
               parts = [| box {| ``type`` = "text"; text = text |} |] |}
    let agentAssistant agent text =
        box {| info = box {| role = "assistant"; finish = "stop"; agent = agent |}
               parts = [| box {| ``type`` = "text"; text = text |} |] |}
    let user text =
        box {| info = box {| role = "user" |}
               parts = [| box {| ``type`` = "text"; text = text |} |] |}

    let text1, agent1, nudged1 = decodeLastAssistant (box [| user "go"; assistant "did work" |])
    equal "last assistant text" "did work" text1
    equal "no agent field → None" None agent1
    check "no trailing nudge → false" (not nudged1)

    let _, _, nudged2 =
        decodeLastAssistant (box [| user "go"; assistant "did work"; user Wanxiangshu.Kernel.PromptFragments.todoNudgePrompt |])
    check "trailing todo nudge → true" nudged2

    let _, _, nudged3 =
        decodeLastAssistant (box [| user "go"; assistant "did work"; user Wanxiangshu.Kernel.PromptFragments.todoNudgePrompt; assistant "more work" |])
    check "assistant after nudge → false" (not nudged3)

    let text4, _, nudged4 =
        decodeLastAssistant (box [| user "go"; assistant "did work"; user Wanxiangshu.Kernel.PromptFragments.todoNudgePrompt; agentAssistant "compaction" "folded history" |])
    equal "compaction assistant ignored as last work" "did work" text4
    check "compaction after nudge preserves already-nudged" nudged4

    let _, _, nudgedEmpty = decodeLastAssistant (box [||])
    check "empty history → false" (not nudgedEmpty)

    let wipToolPart =
        box {| ``type`` = "tool"
               tool = "submit_review"
               callID = "wip-call"
               state = box {| output = submitReviewWipAcknowledgment |} |}
    let assistantWithWipTool =
        box {| info = box {| role = "assistant"; finish = "stop" |}
               parts = [| wipToolPart |] |}
    let _, _, nudgedWipOnly =
        decodeLastAssistant (box [| user "go"; assistant "did work"; assistantWithWipTool |])
    check "wip submit_review tool after work → alreadyNudged false" (not nudgedWipOnly)

    let _, _, nudgedLoopThenWip =
        decodeLastAssistant
            (box
                [| user "go"
                   assistant "did work"
                   user loopNudgePrompt
                   assistantWithWipTool |])
    check "loop nudge then wip tool clears dedup → alreadyNudged false" (not nudgedLoopThenWip)
