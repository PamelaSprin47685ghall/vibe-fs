module Wanxiangshu.Tests.AgentNudgeSpecs

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Runtime.Nudge.NudgeDerivation
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Hosts.Opencode.NudgeTrigger
open Wanxiangshu.Kernel.Nudge.Types
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.ReviewEventWriter
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Hosts.Opencode.NudgeEffect
open Wanxiangshu.Runtime.Dispatch

open Wanxiangshu.Tests.TestWorkspace

let private snap todos msg blocked agent isLoop skipTodo skipReview : Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot =
    { todos = todos
      lastAssistantMessage = msg
      skipTodo = skipTodo
      skipReview = skipReview
      workState = getSessionWorkState false isLoop todos
      blockStatus =
        (if blocked then
             NudgeBlockStatus.Blocked
         else
             NudgeBlockStatus.Allowed)
      nudgeAnchorKey = nudgeAnchorKey "" agent None
      agentFromMessage = agent
      modelFromMessage = None
      reviewLoop = None
      humanTurnId = None }

let private snap' todos msg blocked agent isLoop hasActiveRunner skipTodo skipReview : Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot =
    { todos = todos
      lastAssistantMessage = msg
      skipTodo = skipTodo
      skipReview = skipReview
      workState = getSessionWorkState hasActiveRunner isLoop todos
      blockStatus =
        (if blocked then
             NudgeBlockStatus.Blocked
         else
             NudgeBlockStatus.Allowed)
      nudgeAnchorKey = nudgeAnchorKey "" agent None
      agentFromMessage = agent
      modelFromMessage = None
      reviewLoop = None
      humanTurnId = None }

let private s todos msg blocked agent isLoop =
    snap todos msg blocked agent isLoop false false

let private sSkipTodo todos msg blocked agent isLoop =
    snap todos msg blocked agent isLoop true false

let private sSkipReview todos msg blocked agent isLoop =
    snap todos msg blocked agent isLoop false true

let private sSkipBoth todos msg blocked agent isLoop =
    snap todos msg blocked agent isLoop true true

let test_isNaturalStop () =
    equal
        "session.idle → true"
        true
        (Wanxiangshu.Hosts.Opencode.NudgeTrigger.NudgeTrigger.isNaturalStop "session.idle" null)

    equal
        "session.interrupted → false"
        false
        (Wanxiangshu.Hosts.Opencode.NudgeTrigger.NudgeTrigger.isNaturalStop "session.interrupted" null)

    equal
        "session.error → true"
        true
        (Wanxiangshu.Hosts.Opencode.NudgeTrigger.NudgeTrigger.isNaturalStop "session.error" null)

    equal
        "session.status with string idle → true"
        true
        (Wanxiangshu.Hosts.Opencode.NudgeTrigger.NudgeTrigger.isNaturalStop "session.status" (box {| status = "idle" |}))

    equal
        "session.status with string busy → false"
        false
        (Wanxiangshu.Hosts.Opencode.NudgeTrigger.NudgeTrigger.isNaturalStop "session.status" (box {| status = "busy" |}))

let decision () =
    equal "todos -> NudgeTodo" NudgeTodo (deriveAction (s [ "a" ] "working" false None false))
    equal "todos+skip -> None" NudgeNone (deriveAction (sSkipTodo [ "a" ] "done" false None false))
    equal "nothing -> None" NudgeNone (deriveAction (s [] "ok" false None false))
    equal "loop -> NudgeLoop" NudgeLoop (deriveAction (s [] "ok" false None true))
    equal "loop+emptyText -> NudgeLoop" NudgeLoop (deriveAction (s [] "" false None true))
    equal "todos+emptyText -> NudgeTodo" NudgeTodo (deriveAction (s [ "a" ] "" false None false))
    equal "loop+skip-review -> None" NudgeNone (deriveAction (sSkipReview [] "done" false None true))
    equal "loop+skip-todo still nudges loop" NudgeLoop (deriveAction (sSkipTodo [] "done" false None true))
    equal "todos+skip-review still nudges todo" NudgeTodo (deriveAction (sSkipReview [ "a" ] "done" false None false))
    equal "todos+loop+skip-todo still nudges loop" NudgeLoop (deriveAction (sSkipTodo [ "a" ] "done" false None true))
    equal "todos+loop+skip-review still nudges todo" NudgeTodo (deriveAction (sSkipReview [ "a" ] "done" false None true))
    equal "todos+loop+skip-both -> None" NudgeNone (deriveAction (sSkipBoth [ "a" ] "done" false None true))

    equal
        "todos+activeRunner -> None"
        NudgeNone
        (deriveAction (snap' [ "a" ] "working" false None false true false false))

let dedupFromIntegral () =
    equal "blocked turn -> None" NudgeNone (deriveAction (s [ "a" ] "working" true None false))
    equal "unblocked -> NudgeTodo" NudgeTodo (deriveAction (s [ "a" ] "working" false None false))

let decideNudge' () =
    match deriveAction (s [ "a" ] "working" false None false) with
    | NudgeTodo -> check "fresh turn nudges todo" true
    | _ -> check "fresh turn nudges todo" false

    match deriveAction (s [ "a" ] "working" true None false) with
    | NudgeNone -> check "blocked turn -> StandDown" true
    | _ -> check "blocked turn -> StandDown" false

    match deriveAction (s [] "done" false None false) with
    | NudgeNone -> check "no work -> StandDown" true
    | _ -> check "no work -> StandDown" false

    match deriveAction (s [] "ok" false None true) with
    | NudgeLoop -> check "loop active nudges loop" true
    | _ -> check "loop active nudges loop" false

let selectPrompt () =
    let todoSnapshot = s [ "todo1"; "todo2" ] "working" false None false

    let loopSnapshot =
        let baseSnap = s [] "ok" false None true

        { baseSnap with
            reviewLoop =
                Some
                    { originalTask = "some task"
                      reviewLoopId = "loop-123"
                      currentRound = 1
                      latestVerdict = Some "needs_revision"
                      latestFeedback = Some "add tests" } }

    let noneSnapshot = s [] "done" false None false

    match selectNudgePrompt opencode NudgeTodo todoSnapshot with
    | Some prompt ->
        check "selectNudgePrompt NudgeTodo returns prompt" true
        check "todo prompt contains objective" (prompt.Contains("objective"))
        check "todo prompt contains todo1" (prompt.Contains("todo1"))
        check "todo prompt has background" (prompt.Contains "stream ended")
    | None -> check "selectNudgePrompt NudgeTodo returns prompt" false

    match selectNudgePrompt opencode NudgeLoop loopSnapshot with
    | Some prompt ->
        check "selectNudgePrompt NudgeLoop returns prompt" true
        check "loop prompt contains objective" (prompt.Contains("objective"))
        check "loop prompt excludes review_loop_id key" (not (prompt.Contains "review_loop_id"))
        check "loop prompt excludes loop id value" (not (prompt.Contains "loop-123"))
        check "loop prompt has structured review_mode target" (prompt.Contains "review_mode")
        check "loop prompt has structured latest_verdict target" (prompt.Contains "latest_verdict")
        check "loop prompt has structured latest_feedback target" (prompt.Contains "latest_feedback")
        check "loop prompt background is not prose bag" (not (prompt.Contains "Latest verdict"))
    | None -> check "selectNudgePrompt NudgeLoop returns prompt" false

    match selectNudgePrompt opencode NudgeNone noneSnapshot with
    | None -> check "selectNudgePrompt NudgeNone returns None" true
    | Some _ -> check "selectNudgePrompt NudgeNone returns None" false

let run () =
    decision ()
    dedupFromIntegral ()
    decideNudge' ()
    selectPrompt ()
    test_isNaturalStop ()

let test_dispatchPostStopFromHistory () : JS.Promise<unit> =
    promise {
        let! dir = mkdtempAsync "nudge-integration-test-"
        let sessionIDStr = "s-dispatch-nudge-test"

        let sessionID =
            Wanxiangshu.Kernel.Primitives.Identity.Id.sessionIdQuick sessionIDStr

        let mutable promptCalled = false
        let mutable promptText = ""

        let mockSession =
            createObj
                [ "todo"
                  ==> System.Func<obj, _>(fun arg -> Promise.lift (createObj [ "data" ==> [||] ]))
                  "messages"
                  ==> System.Func<obj, _>(fun arg ->
                      let msg =
                          createObj
                              [ "info" ==> createObj [ "role" ==> "assistant"; "agent" ==> "build" ]
                                "parts"
                                ==> [| createObj [ "type" ==> "text"; "text" ==> "working hard on loop" ] |] ]

                      Promise.lift (createObj [ "data" ==> [| msg |] ]))
                  "prompt"
                  ==> System.Func<obj, _>(fun arg ->
                      promptCalled <- true
                      let body = Dyn.get arg "body"
                      let parts = Dyn.get body "parts"

                      if not (Dyn.isNullish parts) && Dyn.isArray parts then
                          let partsArr = unbox<obj array> parts
                          let firstPart = partsArr.[0]
                          promptText <- Dyn.str firstPart "text"

                          let metadata = Dyn.get firstPart "metadata"
                          let wanxiangshu = Dyn.get metadata "wanxiangshu"
                          let nonce = Dyn.str wanxiangshu "nonce"

                          if nonce <> "" then
                              HostReceiptWaiterRegistry.tryResolve
                                  (Wanxiangshu.Kernel.Primitives.Identity.Id.workspaceIdQuick ("opencode:" + dir))
                                  sessionIDStr
                                  nonce
                                  Wanxiangshu.Kernel.Subsession.Types.OrderedTurnMarkerObserved
                              |> ignore

                      Promise.lift (box {| ok = true |})) ]

        let mockClient = createObj [ "session", box mockSession ]

        let mockPluginCtx =
            createObj [ "client" ==> mockClient; "directory" ==> dir; "sessionID" ==> sessionIDStr ]

        let rt = FallbackRuntimeStore()

        try
            let ndjsonFile = System.IO.Path.Combine(dir, ".wanxiangshu.ndjson")

            if System.IO.File.Exists(ndjsonFile) then
                System.IO.File.Delete(ndjsonFile)

            let lockFile = System.IO.Path.Combine(dir, ".wanxiangshu.ndjson.lock")

            if System.IO.File.Exists(lockFile) then
                System.IO.File.Delete(lockFile)
        with _ ->
            ()

        do! appendLoopActivatedOrFail dir sessionIDStr "fix the issue"

        do!
            dispatchPostStopFromHistory
                Wanxiangshu.Kernel.HostTools.Host.Opencode
                rt
                mockClient
                mockPluginCtx
                sessionID
                (fun _ -> false)

        check "dispatchPostStopFromHistory triggered prompt" promptCalled
        check "promptText is loop nudge" (promptText.Contains("submit_review"))

        try
            let ndjsonFile = System.IO.Path.Combine(dir, ".wanxiangshu.ndjson")

            if System.IO.File.Exists(ndjsonFile) then
                System.IO.File.Delete(ndjsonFile)

            let lockFile = System.IO.Path.Combine(dir, ".wanxiangshu.ndjson.lock")

            if System.IO.File.Exists(lockFile) then
                System.IO.File.Delete(lockFile)
        with _ ->
            ()
    }

let runAsync () =
    promise { do! test_dispatchPostStopFromHistory () }
