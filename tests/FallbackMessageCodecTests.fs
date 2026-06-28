module Wanxiangshu.Tests.FallbackMessageCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.FallbackMessageCodec

let private mkTextMsg (role: string) (text: string) : obj =
    createObj [
        "info", box (createObj [ "role", box role ])
        "parts", box [| box (createObj [ "type", box "text"; "text", box text ]) |]
    ]

let private mkTodoPart (todos: obj array) : obj =
    createObj [
        "type", box "tool"
        "tool", box "task"
        "state", box (createObj [ "input", box (createObj [ "todos", box todos ]) ])
    ]

let private mkTodoMsg (todos: obj array) : obj =
    createObj [ "parts", box [| mkTodoPart todos |] ]

let private mkTodo (status: string) : obj =
    createObj [ "status", box status ]

let hasToolCallAsText_emptyArray () =
    check "empty → false" (not (hasToolCallAsText [||]))

let hasToolCallAsText_noToolCall () =
    let m = mkTextMsg "assistant" "just normal text"
    check "normal text → false" (not (hasToolCallAsText [| m |]))

let hasToolCallAsText_functionEquals () =
    let m = mkTextMsg "assistant" "<function=edit>{\"path\":\"a.fs\"}</function>"
    check "<function= → true" (hasToolCallAsText [| m |])

let hasToolCallAsText_functionSpace () =
    let m = mkTextMsg "assistant" "<function name=\"edit\">...</function>"
    check "<function → true" (hasToolCallAsText [| m |])

let hasToolCallAsText_toolCallTag () =
    let m = mkTextMsg "assistant" "<tool_call>{\"name\":\"edit\",\"arguments\":{}}</tool_call>"
    check "<tool_call> wrapper → true" (hasToolCallAsText [| m |])

let hasToolCallAsText_toolCallTagWithNestedFunction () =
    let m = mkTextMsg "assistant" "<tool_call>\n<function=Task>...</function>\n</tool_call>"
    check "<tool_call>+<function= → true" (hasToolCallAsText [| m |])

let hasToolCallAsText_userMessage () =
    let m = mkTextMsg "user" "<function=edit>...</function>"
    check "user role → false" (not (hasToolCallAsText [| m |]))

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

let run () =
    hasToolCallAsText_emptyArray ()
    hasToolCallAsText_noToolCall ()
    hasToolCallAsText_functionEquals ()
    hasToolCallAsText_functionSpace ()
    hasToolCallAsText_toolCallTag ()
    hasToolCallAsText_toolCallTagWithNestedFunction ()
    hasToolCallAsText_userMessage ()
    allTodosCompleted_emptyArray ()
    allTodosCompleted_allCompleted ()
    allTodosCompleted_mixedStatus ()
    allTodosCompleted_withCancelled ()
    allTodosCompleted_noneCompleted ()
