module Wanxiangshu.Tests.MessagingTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Messaging

let classifySourceEmptyIsNative () =
    equal "native" Native (classifySource "")

let classifySourceUnknownIsNative () =
    equal "native" Native (classifySource "chat-123")

let classifySourceCapsSynthUser () =
    match classifySource "caps-synth-user-xyz" with
    | Synthetic kind -> equal "kind" "caps-synth-user-" kind
    | _ -> failwith "expected synthetic"

let classifySourceCapsSynthAssistant () =
    match classifySource "caps-synth-assistant-xyz" with
    | Synthetic kind -> equal "kind" "caps-synth-assistant-" kind
    | _ -> failwith "expected synthetic"

let classifySourceBacklogProjection () =
    match classifySource "backlog-projection-abc" with
    | Synthetic kind -> check "backlog" (kind.StartsWith "backlog-")
    | _ -> failwith "expected synthetic"

let classifySourceBacklogPrefix () =
    match classifySource "backlog-prefix-xyz" with
    | Synthetic _ -> ()
    | _ -> failwith "expected synthetic"

let classifySourceMagicTodo () =
    match classifySource "magic-todo-projection-x" with
    | Synthetic _ -> ()
    | _ -> failwith "expected synthetic"

let classifySourceMethodologyProbe () =
    match classifySource "methodology-probe-test" with
    | Synthetic _ -> ()
    | _ -> failwith "expected synthetic"

let decodeRoleUser () =
    equal "user" User (decodeRole "user")

let decodeRoleAssistant () =
    equal "assistant" Assistant (decodeRole "assistant")

let decodeRoleToolResult () =
    equal "toolResult" ToolResult (decodeRole "toolResult")

let decodeRoleToolResultHyphen () =
    equal "tool-result" ToolResult (decodeRole "tool-result")

let decodeRoleToolResultUnderscore () =
    equal "tool_result" ToolResult (decodeRole "tool_result")

let decodeRoleUnknownIsSystem () =
    equal "system" System (decodeRole "unknown_role")

let setPartOutputTypedToolPart () =
    let part : Part<obj> = ToolPart("executor", "call1", Some { status = "ok"; output = "old"; error = ""; input = null; operationAction = "" }, null)
    match setPartOutputTyped part "new_output" with
    | ToolPart(_, _, Some st, _) -> equal "new" "new_output" st.output
    | _ -> failwith "expected ToolPart"

let setPartOutputTypedNonToolReturnsAsIs () =
    let part = TextPart "hello"
    match setPartOutputTyped part "ignored" with
    | TextPart t -> equal "hello" "hello" t
    | _ -> failwith "expected TextPart"

let partTextStrTextPart () =
    equal "hello" "hello" (partTextStr (TextPart "hello"))

let partTextStrNonText () =
    equal "" "" (partTextStr (ToolPart("e", "c", None, null) : Part<obj>))

let partIsTextTrue () =
    check "true" (partIsText (TextPart "hello"))

let partIsTextFalse () =
    check "false" (not (partIsText (ToolPart("e", "c", None, null) : Part<obj>)))

let partIsToolTrue () =
    check "true" (partIsTool (ToolPart("e", "c", None, null) : Part<obj>))

let partIsToolFalse () =
    check "false" (not (partIsTool (TextPart "hello")))

let stripSyntheticBySourceKeepsNative () =
    let msg: Message<obj> = { info = { id = ""; sessionID = ""; role = User; agent = ""; isError = false; toolName = ""; details = null; time = null }; parts = []; source = Native; raw = null }
    let result = stripSyntheticBySource [ msg ]
    equal "count" 1 (List.length result)

let stripSyntheticBySourceRemovesSynthetic () =
    let msg: Message<obj> = { info = { id = ""; sessionID = ""; role = User; agent = ""; isError = false; toolName = ""; details = null; time = null }; parts = []; source = Synthetic "caps"; raw = null }
    let result = stripSyntheticBySource [ msg ]
    equal "empty" 0 (List.length result)

let run () =
    classifySourceEmptyIsNative ()
    classifySourceUnknownIsNative ()
    classifySourceCapsSynthUser ()
    classifySourceCapsSynthAssistant ()
    classifySourceBacklogProjection ()
    classifySourceBacklogPrefix ()
    classifySourceMagicTodo ()
    classifySourceMethodologyProbe ()
    decodeRoleUser ()
    decodeRoleAssistant ()
    decodeRoleToolResult ()
    decodeRoleToolResultHyphen ()
    decodeRoleToolResultUnderscore ()
    decodeRoleUnknownIsSystem ()
    setPartOutputTypedToolPart ()
    setPartOutputTypedNonToolReturnsAsIs ()
    partTextStrTextPart ()
    partTextStrNonText ()
    partIsTextTrue ()
    partIsTextFalse ()
    partIsToolTrue ()
    partIsToolFalse ()
    stripSyntheticBySourceKeepsNative ()
    stripSyntheticBySourceRemovesSynthetic ()
