module Wanxiangshu.Tests.IntegrationEventTestsMuxWrappers

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Shell.Dyn

let syntaxWrapperSpec (reg: obj) = promise {
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let sw = wrappers |> Array.find (fun w -> str w "targetTool" = "file_edit_replace_string")
    check "syntax wrapper exists" (not (isNullish sw))
    let mockEdit = createObj [ "execute", box (System.Func<obj, JS.Promise<string>>(fun _ ->
        (promise { return "File written" }))) ]
    let wrapped = (get sw "wrapper") $ (mockEdit, createObj [ "cwd", box "/tmp" ])
    let! _ = (get wrapped "execute") $ (createObj [ "file_path", box "nonexistent.js" ]) |> unbox<JS.Promise<string>>
    check "syntax wrapper returns result" true
}

let todoWriteWrapperSpec (reg: obj) = promise {
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let tw = wrappers |> Array.find (fun w -> str w "targetTool" = "todo_write")
    check "todo_write wrapper exists" (not (isNullish tw))
    let mockTodo =
        createObj
            [ "execute",
              box
                  (System.Func<obj, obj, JS.Promise<obj>>(fun _ _ ->
                      promise { return box {| success = true; output = "Todos updated" |} })) ]
    let wrapped = (get tw "wrapper") $ (mockTodo, createObj [])
    let args =
        createObj
            [ "completedWorkReport", box "integration todo_write wrapper spec handover"
              "select_methodology", box [| "test_driven_reasoning" |]
              "todos",
              box
                  [| createObj
                         [ "content", box "Verify todo_write wrapper"; "status", box "in_progress"; "priority", box "high" ] |] ]
    let opts = createObj [ "toolCallId", box "integration-todo-call-1" ]
    let! result = (get wrapped "execute") $ (args, opts) |> unbox<JS.Promise<obj>>
    check "todo_write wrapper succeeds after codec decode" (truthy (get result "success"))
    let output = str result "output"
    check "todo_write wrapper produces output" (output.Length > 0)
    check "todo_write wrapper includes meditator hint in envelope" (hasExactHint output hintMeditator)
}

let todoWriteWrapperDecodeFailureSpec (reg: obj) = promise {
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let tw = wrappers |> Array.find (fun w -> str w "targetTool" = "todo_write")
    check "todo_write wrapper exists for decode failure" (not (isNullish tw))
    let mockTodo =
        createObj
            [ "execute",
              box
                  (System.Func<obj, obj, JS.Promise<obj>>(fun _ _ ->
                      promise { return box {| success = true; output = "should not run" |} })) ]
    let wrapped = (get tw "wrapper") $ (mockTodo, createObj [])
    let execute = get wrapped "execute"
    let validTodos =
        box
            [| createObj
                   [ "content", box "x"; "status", box "in_progress"; "priority", box "high" ] |]
    let missingReportArgs =
        createObj
            [ "select_methodology", box [| "test_driven_reasoning" |]
              "todos", validTodos ]
    let! r1 = execute $ (missingReportArgs, createObj [ "toolCallId", box "integration-todo-decode-1" ]) |> unbox<JS.Promise<obj>>
    check "todo_write missing completedWorkReport success false" (not (truthy (get r1 "success")))
    let out1 = str r1 "output"
    check "todo_write missing completedWorkReport output invalid" (out1.Contains "invalid")
    check "todo_write missing completedWorkReport names todowrite" (out1.Contains "todowrite")
    let validArgs =
        createObj
            [ "completedWorkReport", box "decode failure spec handover"
              "select_methodology", box [| "test_driven_reasoning" |]
              "todos", validTodos ]
    let! r2 = execute $ (validArgs, createObj []) |> unbox<JS.Promise<obj>>
    check "todo_write missing toolCallId success false" (not (truthy (get r2 "success")))
    let out2 = str r2 "output"
    check "todo_write missing toolCallId output invalid" (out2.Contains "invalid")
    check "todo_write missing toolCallId names todowrite" (out2.Contains "todowrite")
}