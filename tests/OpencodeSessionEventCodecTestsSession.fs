module Wanxiangshu.Tests.OpencodeSessionEventCodecTestsSession

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.OpencodeSessionEventCodec

module Dyn = Wanxiangshu.Shell.Dyn

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

let getSessionIDUsesTopLevelIdForSessionError () =
    let props = box {| id = "sid-top-level" |}
    equal "getSessionID session.error uses props.id" "sid-top-level" (getSessionID "session.error" props)

let getSessionIDReturnsEmptyWhenAbsent () =
    let props = box {| unrelated = "x" |}
    equal "getSessionID empty when no carriers" "" (getSessionID "session.idle" props)

let getSessionIDNonLifecycleSkipsInfoId () =
    let props = box {| info = box {| id = "should-not-use" |} |}
    equal "getSessionID skips info.id for non-lifecycle" "" (getSessionID "session.idle" props)

let getPartsTextEmptyOnNonArray () =
    equal "getPartsText empty on null" "" (getPartsText null)
    equal "getPartsText empty on undefined" "" (getPartsText Wanxiangshu.Shell.Dyn.undefinedValue)

let getPartsTextEmptyOnEmptyArray () =
    equal "getPartsText empty on []" "" (getPartsText [||])

let getPartsTextConcatsTextParts () =
    let parts =
        [| part "first"
           box {| ``type`` = "tool"; tool = "task" |}
           part "second" |]
    equal "getPartsText joins text parts" "first\nsecond" (getPartsText (box parts))

let getPartsTextSkipsNonStringText () =
    let parts = [| box {| ``type`` = "text"; text = Wanxiangshu.Shell.Dyn.undefinedValue |} |]
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

let isCompletedAssistantMessageToolFinishWithTimeCompleted () =
    let info = box {| role = "assistant"; finish = "tool"; time = box {| completed = box 123 |} |}
    check "tool finish with time.completed counts as terminal" (isCompletedAssistantMessage info)

let decodeTodosEmptyOnNonArray () =
    equal "decodeTodos empty on null" [] (decodeTodos null)
    equal "decodeTodos empty on undefined" [] (decodeTodos Wanxiangshu.Shell.Dyn.undefinedValue)

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
    let text, agent = decodeLastAssistant (box [| msg |])
    equal "text empty when no assistant" "" text
    equal "agent None when no assistant" true (Option.isNone agent)

let decodeLastAssistantReturnsLastTextAndAgent () =
    let info = box {| role = "assistant"; agent = "coder"; finish = "stop" |}
    let msg1 = messageOf (box {| role = "user" |}) (box [| part "ignored" |])
    let msg2 = messageOf info (box [| part "answer" |])
    let text, agent = decodeLastAssistant (box [| msg1; msg2 |])
    equal "text from last assistant" "answer" text
    equal "agent captured" (Some "coder") agent

let decodeLastAssistantDetectsSyntheticAgent () =
    let info = box {| role = "assistant"; agent = "compaction"; finish = "stop" |}
    let msg = messageOf info (box [| part "should ignore" |])
    let text, _ = decodeLastAssistant (box [| msg |])
    equal "synthetic agent skipped" "" text

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

let shouldSkipNudgeTrueWhenNoToolResult () =
    let msg1 = messageOf (box {| role = "user" |}) (box [| part "hi" |])
    let msg2 = messageOf (box {| role = "assistant"; finish = "tool" |}) (box [| part "call submit_review" |])
    let messages = box [| msg1; msg2 |]
    check "shouldSkipNudge is true when no toolResult follow tool finish" (shouldSkipNudge messages)

let shouldSkipNudgeFalseWhenToolResultPresent () =
    let msg1 = messageOf (box {| role = "user" |}) (box [| part "hi" |])
    let msg2 = messageOf (box {| role = "assistant"; finish = "tool" |}) (box [| part "call submit_review" |])
    let msg3 = messageOf (box {| role = "toolResult" |}) (box [| part "rejected" |])
    let messages = box [| msg1; msg2; msg3 |]
    check "shouldSkipNudge is false when toolResult is present after tool finish" (not (shouldSkipNudge messages))