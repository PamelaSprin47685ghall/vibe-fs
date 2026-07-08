module Wanxiangshu.Tests.FallbackMessageCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.FallbackMessageCodec

let private mkTodoPart (todos: obj array) : obj =
    createObj
        [ "type", box "tool"
          "tool", box "task"
          "state", box (createObj [ "input", box (createObj [ "todos", box todos ]) ]) ]

let private mkTodoMsg (todos: obj array) : obj =
    createObj [ "parts", box [| mkTodoPart todos |] ]

let private mkTodo (status: string) : obj = createObj [ "status", box status ]

let private mkAssistantTextMsg (text: string) : obj =
    createObj
        [ "info", box (createObj [ "role", box "assistant" ])
          "parts", box [| createObj [ "type", box "text"; "text", box text ] |] ]

let allTodosCompleted_emptyArray () =
    check "empty → false" (not (allTodosCompleted [||]))

let allTodosCompleted_allCompleted () =
    let m = mkTodoMsg [| mkTodo "completed"; mkTodo "completed" |]
    check "all completed → true" (allTodosCompleted [| m |])

let allTodosCompleted_mixedStatus () =
    let m = mkTodoMsg [| mkTodo "completed"; mkTodo "in_progress" |]
    check "mixed → false" (not (allTodosCompleted [| m |]))

let allTodosCompleted_withCancelled () =
    let m = mkTodoMsg [| mkTodo "completed"; mkTodo "cancelled" |]
    check "completed+cancelled → true" (allTodosCompleted [| m |])

let allTodosCompleted_noneCompleted () =
    let m = mkTodoMsg [| mkTodo "in_progress"; mkTodo "pending" |]
    check "none completed → false" (not (allTodosCompleted [| m |]))

let containsToolCallAsText_functionEqualsPattern () =
    check
        "<function=read> -> true"
        (containsToolCallAsText "<function=read>\n<parameter=filePath>\n/foo/bar.fs\n</parameter>\n</function>")

let containsToolCallAsText_noXml () =
    check "plain text → false" (not (containsToolCallAsText "just some normal text"))

let containsToolCallAsText_toolCallTag () =
    check "<tool_call> → true" (containsToolCallAsText "let me use <tool_call>name=\"read\"</tool_call> to read")

let containsToolCallAsText_functionTag () =
    check "<function> → true" (containsToolCallAsText "<function>read</function>")

let containsToolCallAsText_toolNameTag () =
    check "<read> → true" (containsToolCallAsText "<read file_path=\"/foo\">")

let containsToolCallAsText_nameAttribute () =
    check "name=\"write\" → true" (containsToolCallAsText "<invoke name=\"write\">content</invoke>")

let scanToolCallAsText_noMessages () =
    check "empty → None" (None = (scanToolCallAsText [||]))

let scanToolCallAsText_noAssistantText () =
    let userMsg =
        createObj
            [ "info", box (createObj [ "role", box "user" ])
              "parts", box [| createObj [ "type", box "text"; "text", box "<read>" ] |] ]

    check "user text → None" (None = (scanToolCallAsText [| userMsg |]))

let scanToolCallAsText_assistantTextWithToolCall () =
    let prompt =
        scanToolCallAsText [| mkAssistantTextMsg "I'll use <tool_call> to read" |]

    check "assistant with tool call → Some" (Option.isSome prompt)

let scanToolCallAsText_assistantTextClean () =
    let prompt =
        scanToolCallAsText [| mkAssistantTextMsg "I'll read this file properly." |]

    check "assistant clean text → None" (None = prompt)

let run () =
    allTodosCompleted_emptyArray ()
    allTodosCompleted_allCompleted ()
    allTodosCompleted_mixedStatus ()
    allTodosCompleted_withCancelled ()
    allTodosCompleted_noneCompleted ()
    containsToolCallAsText_functionEqualsPattern ()
    containsToolCallAsText_noXml ()
    containsToolCallAsText_toolCallTag ()
    containsToolCallAsText_functionTag ()
    containsToolCallAsText_toolNameTag ()
    containsToolCallAsText_nameAttribute ()
    scanToolCallAsText_noMessages ()
    scanToolCallAsText_noAssistantText ()
    scanToolCallAsText_assistantTextWithToolCall ()
    scanToolCallAsText_assistantTextClean ()
