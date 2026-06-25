module VibeFs.Tests.OpencodeSessionEventCodecTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Shell.OpencodeSessionEventCodec
open VibeFs.Kernel.NudgeState

// `Dyn` short prefix collides with `VibeFs.Tests.DynTests` in the test
// namespace, so use an explicit alias for the Shell decoder.
module Dyn = VibeFs.Shell.Dyn

let private part (text: string) : obj = box {| ``type`` = "text"; text = text |}

let private taskPart (todos: obj array) : obj =
    box {|
        ``type`` = "tool"
        tool = "task"
        state = box {| input = box {| todos = box todos |} |}
    |}

let private messageOf (info: obj) (parts: obj) : obj =
    box {| info = info; parts = parts |}

let getSessionIDFromPropsPrefersPropsKey () =
    let props = box {| sessionID = "from-props" |}
    equal "getSessionID picks props.sessionID first" "from-props" (getSessionID "session.idle" props)

let getSessionIDFallsBackToPart () =
    let props = box {| part = box {| sessionID = "from-part" |} |}
    equal "getSessionID falls back to part.sessionID" "from-part" (getSessionID "message.updated" props)

let getSessionIDFallsBackToInfo () =
    let props = box {| info = box {| sessionID = "from-info" |} |}
    equal "getSessionID falls back to info.sessionID" "from-info" (getSessionID "session.error" props)

let getSessionIDLifecycleUsesInfoId () =
    let props = box {| info = box {| id = "from-info-id" |} |}
    equal "getSessionID uses info.id for lifecycle events" "from-info-id" (getSessionID "session.deleted" props)
    equal "getSessionID uses info.id for session.created" "from-info-id" (getSessionID "session.created" props)

let getSessionIDReturnsEmptyWhenAbsent () =
    let props = box {| unrelated = "x" |}
    equal "getSessionID empty when no carriers" "" (getSessionID "session.idle" props)

let getSessionIDNonLifecycleSkipsInfoId () =
    let props = box {| info = box {| id = "should-not-use" |} |}
    equal "getSessionID skips info.id for non-lifecycle" "" (getSessionID "session.idle" props)

let getPartsTextEmptyOnNonArray () =
    equal "getPartsText empty on null" "" (getPartsText null)
    equal "getPartsText empty on undefined" "" (getPartsText VibeFs.Shell.Dyn.undefinedValue)

let getPartsTextEmptyOnEmptyArray () =
    equal "getPartsText empty on []" "" (getPartsText [||])

let getPartsTextConcatsTextParts () =
    let parts =
        [| part "first"
           box {| ``type`` = "tool"; tool = "task" |}
           part "second" |]
    equal "getPartsText joins text parts" "first\nsecond" (getPartsText (box parts))

let getPartsTextSkipsNonStringText () =
    let parts = [| box {| ``type`` = "text"; text = VibeFs.Shell.Dyn.undefinedValue |} |]
    equal "getPartsText skips non-string text payloads" "" (getPartsText (box parts))

let isCompletedAssistantMessageNonAssistant () =
    check "user role not completed" (not (isCompletedAssistantMessage (box {| role = "user" |})))

let isCompletedAssistantMessageWithError () =
    let info = box {| role = "assistant"; error = box "boom" |}
    check "assistant with error not completed" (not (isCompletedAssistantMessage info))

let isCompletedAssistantMessageTerminalFinish () =
    let info = box {| role = "assistant"; finish = "stop" |}
    check "assistant with terminal finish completed" (isCompletedAssistantMessage info)

let isCompletedAssistantMessageToolFinishNotTerminal () =
    let info = box {| role = "assistant"; finish = "tool-calls" |}
    check "tool-calls finish not terminal" (not (isCompletedAssistantMessage info))

let isCompletedAssistantMessageTimeCompleted () =
    let info = box {| role = "assistant"; time = box {| completed = box 123 |} |}
    check "time.completed numeric counts as terminal" (isCompletedAssistantMessage info)

let decodeTodosEmptyOnNonArray () =
    equal "decodeTodos empty on null" [] (decodeTodos null)
    equal "decodeTodos empty on undefined" [] (decodeTodos VibeFs.Shell.Dyn.undefinedValue)

let decodeTodosDropsTerminalStatus () =
    let todos =
        [| box {| content = "stay"; status = "pending" |}
           box {| content = "drop"; status = "completed" |}
           box {| content = "stay2"; status = "in_progress" |}
           box {| content = "drop2"; status = "cancelled" |} |]
    equal "decodeTodos keeps non-terminal" [ "stay"; "stay2" ] (decodeTodos (box todos))

let decodeTodosDropsEmptyContent () =
    let todos = [| box {| content = ""; status = "pending" |} |]
    equal "decodeTodos drops empty content" [] (decodeTodos (box todos))

let recoverOpenTodosFromMessagesEmptyOnNonArray () =
    equal "recover empty on null" [] (recoverOpenTodosFromMessages null)

let recoverOpenTodosFromMessagesEmptyWhenNoTaskPart () =
    let msg = messageOf (box {| role = "user" |}) (box [| part "hello" |])
    equal "recover empty when no task part" [] (recoverOpenTodosFromMessages (box [| msg |]))

let recoverOpenTodosFromMessagesPicksLatestTaskPart () =
    let todo1 = taskPart [| box {| content = "older"; status = "completed" |} |]
    let todo2 = taskPart [| box {| content = "newer"; status = "pending" |} |]
    let msg1 = messageOf (box {| role = "user" |}) (box [| todo1 |])
    let msg2 = messageOf (box {| role = "assistant" |}) (box [| todo2 |])
    equal "recover picks open todos from latest task part"
        [ "newer" ]
        (recoverOpenTodosFromMessages (box [| msg1; msg2 |]))

let recoverOpenTodosFromMessagesDropsTerminal () =
    let todos = taskPart [| box {| content = "keep"; status = "pending" |}
                            box {| content = "drop"; status = "completed" |} |]
    let msg = messageOf (box {| role = "user" |}) (box [| todos |])
    equal "recover drops terminal todos" [ "keep" ] (recoverOpenTodosFromMessages (box [| msg |]))

let decodeLastAssistantEmptyWhenNoCompletedAssistant () =
    let msg = messageOf (box {| role = "user" |}) (box [| part "hi" |])
    let text, agent, nudged = decodeLastAssistant (box [| msg |])
    equal "text empty when no assistant" "" text
    equal "agent None when no assistant" true (Option.isNone agent)
    equal "alreadyNudged false when no assistant" false nudged

let decodeLastAssistantReturnsLastTextAndAgent () =
    let info = box {| role = "assistant"; agent = "coder"; finish = "stop" |}
    let msg1 = messageOf (box {| role = "user" |}) (box [| part "ignored" |])
    let msg2 = messageOf info (box [| part "answer" |])
    let text, agent, nudged = decodeLastAssistant (box [| msg1; msg2 |])
    equal "text from last assistant" "answer" text
    equal "agent captured" (Some "coder") agent
    equal "no nudge prompt follows" false nudged

let decodeLastAssistantDetectsSyntheticAgent () =
    let info = box {| role = "assistant"; agent = "compaction"; finish = "stop" |}
    let msg = messageOf info (box [| part "should ignore" |])
    let text, _, _ = decodeLastAssistant (box [| msg |])
    equal "synthetic agent skipped" "" text

let decodeLastAssistantDetectsAlreadyNudged () =
    let info = box {| role = "assistant"; finish = "stop" |}
    let msgAssistant = messageOf info (box [| part "done" |])
    let nudgePart = part VibeFs.Kernel.PromptFragments.todoNudgePrompt
    let msgNudge = messageOf (box {| role = "assistant" |}) (box [| nudgePart |])
    let _, _, nudged = decodeLastAssistant (box [| msgAssistant; msgNudge |])
    check "already-nudged detected" nudged

let createPromptBodyWithoutAgent () =
    let body = createPromptBody None "hello"
    let hasAgent = Dyn.has body "agent"
    check "body without agent has no agent field" (not hasAgent)
    let parts = Dyn.get body "parts"
    check "body has parts" (Dyn.isArray parts)

let createPromptBodyWithAgent () =
    let body = createPromptBody (Some "reviewer") "hello"
    let agent = Dyn.str body "agent"
    equal "body agent captured" "reviewer" agent

let decodeNudgeHostEventStreamAbort () =
    let ev = decodeNudgeHostEvent "stream-abort" (box {| |})
    check "decodeNudgeHostEvent stream-abort" (match ev with StreamAbort -> true | _ -> false)

let decodeNudgeHostEventSessionLifecyclePure () =
    let ev = decodeNudgeHostEvent "session.deleted" (box {| |})
    check "decodeNudgeHostEvent session.deleted" (match ev with SessionDeleted -> true | _ -> false)

let decodeNudgeHostEventSessionIdle () =
    let ev = decodeNudgeHostEvent "session.idle" (box {| |})
    check "decodeNudgeHostEvent session.idle" (match ev with SessionIdle -> true | _ -> false)

let decodeNudgeHostEventRetryProgressByName () =
    let ev = decodeNudgeHostEvent "session.next.text.delta" (box {| |})
    check "decodeNudgeHostEvent retry-progress" (match ev with RetryProgress -> true | _ -> false)

let decodeNudgeHostEventUnknown () =
    let ev = decodeNudgeHostEvent "unknown.event" (box {| |})
    check "decodeNudgeHostEvent unknown falls to Other" (match ev with Other -> true | _ -> false)

let decodeNudgeHostEventMessageUpdatedCompleted () =
    let props =
        box {|
            info = box {| role = "assistant"; finish = "stop" |}
        |}
    let ev = decodeNudgeHostEvent "message.updated" props
    check "decodeNudgeHostEvent message.updated completed"
        (match ev with MessageUpdated UpdateCompletedAssistant -> true | _ -> false)

let decodeNudgeHostEventMessageUpdatedAborted () =
    let props =
        box {|
            info = box {| role = "assistant"; error = box {| name = "MessageAbortedError" |} |}
        |}
    let ev = decodeNudgeHostEvent "message.updated" props
    check "decodeNudgeHostEvent message.updated aborted"
        (match ev with MessageUpdated UpdateAborted -> true | _ -> false)

let decodeNudgeHostEventMessageUpdatedNoChange () =
    let props =
        box {|
            info = box {| role = "user" |}
        |}
    let ev = decodeNudgeHostEvent "message.updated" props
    check "decodeNudgeHostEvent message.updated nochange"
        (match ev with MessageUpdated UpdateNoChange -> true | _ -> false)

let decodeNudgeHostEventMessagePartUpdatedRetry () =
    let props = box {| part = box {| ``type`` = "retry" |} |}
    let ev = decodeNudgeHostEvent "message.part.updated" props
    check "decodeNudgeHostEvent message.part.updated retry"
        (match ev with MessagePartUpdated PartRetry -> true | _ -> false)

let decodeNudgeHostEventMessagePartUpdatedAborted () =
    let props = box {| part = box {| ``type`` = "tool"; error = box {| name = "MessageAbortedError" |} |} |}
    let ev = decodeNudgeHostEvent "message.part.updated" props
    check "decodeNudgeHostEvent message.part.updated aborted"
        (match ev with MessagePartUpdated PartAborted -> true | _ -> false)

let decodeNudgeHostEventSessionStatusBusy () =
    let props = box {| status = box {| ``type`` = "busy" |} |}
    let ev = decodeNudgeHostEvent "session.status" props
    check "decodeNudgeHostEvent session.status busy"
        (match ev with SessionStatusBusy -> true | _ -> false)

let decodeNudgeHostEventSessionStatusUnknownFallsToOther () =
    let props = box {| status = box {| ``type`` = "garbage" |} |}
    let ev = decodeNudgeHostEvent "session.status" props
    check "decodeNudgeHostEvent session.status unknown falls to Other"
        (match ev with Other -> true | _ -> false)

let decodeNudgeHostEventSessionPromptedFromPartsFallback () =
    let props = box {| parts = box [| part "hello world" |] |}
    let ev = decodeNudgeHostEvent "session.next.prompted" props
    check "decodeNudgeHostEvent session.next.prompted from parts"
        (match ev with SessionNextPrompted text when text = "hello world" -> true | _ -> false)

let decodeNudgeHostEventSessionPromptedFromPromptText () =
    let props = box {| prompt = box {| text = "direct text" |} |}
    let ev = decodeNudgeHostEvent "session.next.prompted" props
    check "decodeNudgeHostEvent session.next.prompted from prompt"
        (match ev with SessionNextPrompted text when text = "direct text" -> true | _ -> false)

let run () =
    getSessionIDFromPropsPrefersPropsKey ()
    getSessionIDFallsBackToPart ()
    getSessionIDFallsBackToInfo ()
    getSessionIDLifecycleUsesInfoId ()
    getSessionIDReturnsEmptyWhenAbsent ()
    getSessionIDNonLifecycleSkipsInfoId ()
    getPartsTextEmptyOnNonArray ()
    getPartsTextEmptyOnEmptyArray ()
    getPartsTextConcatsTextParts ()
    getPartsTextSkipsNonStringText ()
    isCompletedAssistantMessageNonAssistant ()
    isCompletedAssistantMessageWithError ()
    isCompletedAssistantMessageTerminalFinish ()
    isCompletedAssistantMessageToolFinishNotTerminal ()
    isCompletedAssistantMessageTimeCompleted ()
    decodeTodosEmptyOnNonArray ()
    decodeTodosDropsTerminalStatus ()
    decodeTodosDropsEmptyContent ()
    recoverOpenTodosFromMessagesEmptyOnNonArray ()
    recoverOpenTodosFromMessagesEmptyWhenNoTaskPart ()
    recoverOpenTodosFromMessagesPicksLatestTaskPart ()
    recoverOpenTodosFromMessagesDropsTerminal ()
    decodeLastAssistantEmptyWhenNoCompletedAssistant ()
    decodeLastAssistantReturnsLastTextAndAgent ()
    decodeLastAssistantDetectsSyntheticAgent ()
    decodeLastAssistantDetectsAlreadyNudged ()
    createPromptBodyWithoutAgent ()
    createPromptBodyWithAgent ()
    decodeNudgeHostEventStreamAbort ()
    decodeNudgeHostEventSessionLifecyclePure ()
    decodeNudgeHostEventSessionIdle ()
    decodeNudgeHostEventRetryProgressByName ()
    decodeNudgeHostEventUnknown ()
    decodeNudgeHostEventMessageUpdatedCompleted ()
    decodeNudgeHostEventMessageUpdatedAborted ()
    decodeNudgeHostEventMessageUpdatedNoChange ()
    decodeNudgeHostEventMessagePartUpdatedRetry ()
    decodeNudgeHostEventMessagePartUpdatedAborted ()
    decodeNudgeHostEventSessionStatusBusy ()
    decodeNudgeHostEventSessionStatusUnknownFallsToOther ()
    decodeNudgeHostEventSessionPromptedFromPartsFallback ()
    decodeNudgeHostEventSessionPromptedFromPromptText ()