module Wanxiangshu.Tests.IntegrationMuxToolSpecsTodo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Tests.IntegrationMuxSetup
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Mux.Plugin
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.Dyn

let muxTodoWriteWrapperSchemaSpec () = promise {
    let reg = sharedMuxRegistration ()
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let todoWrapper = wrappers |> Array.tryFind (fun w -> str w "targetTool" = "todo_write")
    if isNullish todoWrapper then
        check "mux registration exposes todo_write wrapper" false
    else
        let fakeHostTodo =
            box
                {| execute =
                    System.Func<obj, obj, JS.Promise<obj>>(fun args _opts ->
                        promise { return box {| success = true; count = (unbox<obj[]> (get args "todos")).Length |} }) |}
        let wrapped = (get todoWrapper "wrapper") $ (fakeHostTodo, createObj [])
        let schema = get wrapped "parameters"
        let properties = get schema "properties"
        let todosProp = get properties "todos"
        let todoItem = get todosProp "items"
        let todoProps = get todoItem "properties"
        let required = unbox<string[]> (get schema "required")
        let todoRequired = unbox<string[]> (get todoItem "required")
        check "mux todo_write wrapper requires completedWorkReport" (required |> Array.contains "completedWorkReport")
        check "mux todo_write wrapper exposes priority" (not (isNullish (get todoProps "priority")))
        check "mux todo_write wrapper requires priority" (todoRequired |> Array.contains "priority")
}

let muxTodoWriteCapturesCompletedWorkReportSpec () = promise {
    let reg = sharedMuxRegistration ()
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let todoWrapper = wrappers |> Array.tryFind (fun w -> str w "targetTool" = "todo_write")
    if isNullish todoWrapper then
        check "mux registration exposes todo_write wrapper for capture" false
    else
        let mutable nativeArgs = null
        let fakeHostTodo =
            box
                {| execute =
                    System.Func<obj, obj, JS.Promise<obj>>(fun args _opts ->
                        nativeArgs <- args
                        promise { return box {| success = true; count = (unbox<obj[]> (get args "todos")).Length |} }) |}
        let wrapped = (get todoWrapper "wrapper") $ (fakeHostTodo, createObj [])
        let execute = get wrapped "execute"
        let args =
            createObj
                [ "completedWorkReport", box "finished wrapper capture"
                  "todos",
                  box
                      [| createObj [ "content", box "Inspect wrapper"; "status", box "in_progress"; "priority", box "high" ] |] ]
        let! result = (execute $ (args, createObj [ "toolCallId", box "todo-call-1" ])) |> unbox<JS.Promise<obj>>
        let nativeTodos = unbox<obj[]> (get nativeArgs "todos")
        check "mux todo_write wrapper strips completedWorkReport before native execute" (isNullish (get nativeArgs "completedWorkReport"))
        check "mux todo_write wrapper strips priority before native execute" (isNullish (get nativeTodos.[0] "priority"))
        let scope = unbox<RuntimeScope> (get reg "__runtimeScope")
        check "mux todo_write wrapper captures completedWorkReport" (scope.Projection.TryGetReport(mux, "todo-call-1") = Some "finished wrapper capture")
        check "mux todo_write wrapper keeps nudge behavior" (hasExactHint (str result "output") hintMeditator)
}

let muxBacklogProjectionSpec () = promise {
    let reg = sharedMuxRegistration ()
    let tf = muxMessageTransform reg
    if isNullish tf then
        check "mux messagesTransform exposed for magic todo projection" false
    else
        let todoInput report content status priority =
            createObj
                [ "completedWorkReport", box report
                  "todos", box [| createObj [ "content", box content; "status", box status; "priority", box priority ] |] ]
        let todoOutput count = createObj [ "success", box true; "count", box count ]
        let messages =
            [| muxTextMessage "todo-user-1" "user" "plan phase"
               muxDynamicToolMessage "todo-1" "todo_write" "todo-call-a" (todoInput "planned phase" "Plan change" "in_progress" "high") (todoOutput 1)
               muxTextMessage "todo-user-2" "user" "implemented phase"
               muxDynamicToolMessage "todo-2" "todo_write" "todo-call-b" (todoInput "implemented phase" "Implement change" "completed" "high") (todoOutput 1)
               muxTextMessage "todo-user-3" "user" "verified phase"
               muxDynamicToolMessage "todo-3" "todo_write" "todo-call-c" (todoInput "verified phase" "Verify change" "completed" "medium") (todoOutput 1) |]
        let out = createObj [ "messages", box messages ]
        let input = createObj [ "agent", box "manager"; "sessionID", box "mux-magic-todo-session" ]
        do! (tf $ (input, out)) |> unbox<JS.Promise<unit>>
        let transformed = unbox<obj[]> (get out "messages")
        let texts =
            transformed
            |> Array.collect (fun msg ->
                let parts = unbox<obj[]> (get msg "parts")
                parts
                |> Array.choose (fun part -> if str part "type" = "text" then Some (str part "text") else None))
        check "mux magic todo projection injects folded backlog text" (
            texts
            |> Array.exists (fun text ->
                text.Contains("Completed work from folded turns. File changes are already on disk.")
                && text.Contains("user_message:")
                && text.Contains("planned phase")))
}