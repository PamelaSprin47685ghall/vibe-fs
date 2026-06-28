module Wanxiangshu.Tests.FallbackMessageCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.FallbackMessageCodec

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
    allTodosCompleted_emptyArray ()
    allTodosCompleted_allCompleted ()
    allTodosCompleted_mixedStatus ()
    allTodosCompleted_withCancelled ()
    allTodosCompleted_noneCompleted ()
