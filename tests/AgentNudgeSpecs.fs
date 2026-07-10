module Wanxiangshu.Tests.AgentNudgeSpecs

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeDerivation
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Opencode.NudgeTrigger
open Wanxiangshu.Kernel.Nudge.Types
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Opencode.NudgeEffect
open Wanxiangshu.Tests.TempWorkspace

let private snap todos msg blocked agent isLoop : Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot =
    { todos = todos
      lastAssistantMessage = msg
      workState = getSessionWorkState false isLoop todos
      blockStatus =
        (if blocked then
             NudgeBlockStatus.Blocked
         else
             NudgeBlockStatus.Allowed)
      nudgeAnchorKey = msg
      agentFromMessage = agent
      modelFromMessage = None }

let private snap' todos msg blocked agent isLoop hasActiveRunner : Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot =
    { todos = todos
      lastAssistantMessage = msg
      workState = getSessionWorkState hasActiveRunner isLoop todos
      blockStatus =
        (if blocked then
             NudgeBlockStatus.Blocked
         else
             NudgeBlockStatus.Allowed)
      nudgeAnchorKey = msg
      agentFromMessage = agent
      modelFromMessage = None }

let test_isNaturalStop () =
    equal "session.idle → true" true (Wanxiangshu.Opencode.NudgeTrigger.NudgeTrigger.isNaturalStop "session.idle" null)

    equal
        "session.interrupted → false"
        false
        (Wanxiangshu.Opencode.NudgeTrigger.NudgeTrigger.isNaturalStop "session.interrupted" null)

    equal
        "session.error → true"
        true
        (Wanxiangshu.Opencode.NudgeTrigger.NudgeTrigger.isNaturalStop "session.error" null)

    equal
        "session.status with string idle → true"
        true
        (Wanxiangshu.Opencode.NudgeTrigger.NudgeTrigger.isNaturalStop "session.status" (box {| status = "idle" |}))

    equal
        "session.status with string busy → false"
        false
        (Wanxiangshu.Opencode.NudgeTrigger.NudgeTrigger.isNaturalStop "session.status" (box {| status = "busy" |}))

let decision () =
    equal "todos -> NudgeTodo" NudgeTodo (deriveAction (snap [ "a" ] "working" false None false))
    equal "todos+skip -> None" NudgeNone (deriveAction (snap [ "a" ] "done <skip-todo-check />" false None false))

    equal
        "todos+skip+unclosedFence -> None"
        NudgeNone
        (deriveAction (
            snap [ "a" ] "I will skip this turn. <skip-todo-check />\n\n```fsharp\nlet x = 1\n" false None false
        ))

    equal "nothing -> None" NudgeNone (deriveAction (snap [] "ok" false None false))
    equal "loop -> NudgeLoop" NudgeLoop (deriveAction (snap [] "ok" false None true))
    equal "loop+emptyText -> NudgeLoop" NudgeLoop (deriveAction (snap [] "" false None true))
    equal "todos+emptyText -> NudgeTodo" NudgeTodo (deriveAction (snap [ "a" ] "" false None false))
    equal "loop+skip-review -> None" NudgeNone (deriveAction (snap [] "done <skip-review-check />" false None true))

    equal
        "loop+skip-todo still nudges loop"
        NudgeLoop
        (deriveAction (snap [] "done <skip-todo-check />" false None true))

    equal
        "todos+skip-review still nudges todo"
        NudgeTodo
        (deriveAction (snap [ "a" ] "done <skip-review-check />" false None false))

    equal
        "todos+loop+skip-todo still nudges loop"
        NudgeLoop
        (deriveAction (snap [ "a" ] "done <skip-todo-check />" false None true))

    equal
        "todos+loop+skip-review still nudges todo"
        NudgeTodo
        (deriveAction (snap [ "a" ] "done <skip-review-check />" false None true))

    equal
        "todos+loop+skip-both -> None"
        NudgeNone
        (deriveAction (snap [ "a" ] "done <skip-todo-check /><skip-review-check />" false None true))

    equal "todos+activeRunner -> None" NudgeNone (deriveAction (snap' [ "a" ] "working" false None false true))

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

    match selectNudgePrompt opencode NudgeTodo todoSnapshot with
    | Some prompt ->
        check "selectNudgePrompt NudgeTodo returns prompt" true
        check "todo prompt contains front matter" (prompt.Contains("---"))
        check "todo prompt contains todos" (prompt.Contains("todos"))
        check "todo prompt contains todo content" (prompt.Contains("todo1"))
    | None -> check "selectNudgePrompt NudgeTodo returns prompt" false

    match selectNudgePrompt opencode NudgeLoop loopSnapshot with
    | Some prompt ->
        check "selectNudgePrompt NudgeLoop returns prompt" true
        check "loop prompt does not contain front matter" (not (prompt.Contains("---")))
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
        let sessionID = Wanxiangshu.Kernel.Domain.Id.sessionIdQuick sessionIDStr

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

                      Promise.lift (box {| ok = true |})) ]

        let mockClient = createObj [ "session", box mockSession ]

        let mockPluginCtx =
            createObj [ "client" ==> mockClient; "directory" ==> dir; "sessionID" ==> sessionIDStr ]

        let rt = FallbackRuntimeState()

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
        check "promptText is loop nudge" (promptText.Contains("You are in loop mode. You must call the submit_review"))

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
