module Wanxiangshu.Tests.IntegrationMuxToolSpecsTodo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Tests.IntegrationMuxSetup
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Hosts.Mux.Plugin
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Tests.TempWorkspace

let muxTodoWriteWrapperSchemaSpec () =
    promise {
        let reg = sharedMuxRegistration ()
        let wrappers = unbox<obj[]> (get reg "wrappers")

        let todoWrapper =
            wrappers |> Array.tryFind (fun w -> str w "targetTool" = "todo_write")

        if isNullish todoWrapper then
            check "mux registration exposes todo_write wrapper" false
        else
            let fakeHostTodo =
                box
                    {| execute =
                        System.Func<obj, obj, JS.Promise<obj>>(fun args _opts ->
                            promise {
                                return
                                    box
                                        {| success = true
                                           count = (unbox<obj[]> (get args "todos")).Length |}
                            }) |}

            let wrapped = (get todoWrapper "wrapper") $ (fakeHostTodo, createObj [])
            let schema = get wrapped "parameters"
            let properties = get schema "properties"
            let todosProp = get properties "todos"
            let todoItem = get todosProp "items"
            let todoProps = get todoItem "properties"
            let required = unbox<string[]> (get schema "required")
            let todoRequired = unbox<string[]> (get todoItem "required")
            check "mux todo_write wrapper does NOT require ahaMoments" (not (required |> Array.contains "ahaMoments"))
            check "mux todo_write wrapper exposes priority" (not (isNullish (get todoProps "priority")))
            check "mux todo_write wrapper requires priority" (todoRequired |> Array.contains "priority")
    }

let muxTodoWriteCapturesCompletedWorkReportSpec () =
    promise {
        let seams = sharedMuxRegistrationWithSeams ()
        let reg = seams.Registration
        let scope = seams.Scope
        let wrappers = unbox<obj[]> (get reg "wrappers")

        let todoWrapper =
            wrappers |> Array.tryFind (fun w -> str w "targetTool" = "todo_write")

        if isNullish todoWrapper then
            check "mux registration exposes todo_write wrapper for capture" false
        else
            let mutable nativeArgs = null

            let fakeHostTodo =
                box
                    {| execute =
                        System.Func<obj, obj, JS.Promise<obj>>(fun args _opts ->
                            nativeArgs <- args

                            promise {
                                return
                                    box
                                        {| success = true
                                           count = (unbox<obj[]> (get args "todos")).Length |}
                            }) |}

            let wrapped = (get todoWrapper "wrapper") $ (fakeHostTodo, createObj [])
            let execute = get wrapped "execute"

            let args =
                createObj
                    [ "ahaMoments", box (System.String('a', 1024))
                      "changesAndReasons", box (System.String('b', 1024))
                      "gotchas", box (System.String('c', 1024))
                      "lessonsAndConventions", box (System.String('d', 1024))
                      "plan", box (System.String('e', 1024))
                      "select_methodology", box [| "first_principles" |]
                      "todos",
                      box
                          [| createObj
                                 [ "content", box "Inspect wrapper"
                                   "status", box "in_progress"
                                   "priority", box "high" ] |] ]

            let! result =
                (execute $ (args, createObj [ "toolCallId", box "todo-call-1" ]))
                |> unbox<JS.Promise<obj>>

            let nativeTodos = unbox<obj[]> (get nativeArgs "todos")

            check
                "mux todo_write wrapper strips ahaMoments before native execute"
                (isNullish (get nativeArgs "ahaMoments"))

            check
                "mux todo_write wrapper strips priority before native execute"
                (isNullish (get nativeTodos.[0] "priority"))

            let captured = scope.Projection.TryGetBacklogEntry(mux, "todo-call-1")

            check
                "mux todo_write wrapper captures ahaMoments"
                (captured = Some
                                { ahaMoments = System.String('a', 1024)
                                  changesAndReasons = System.String('b', 1024)
                                  gotchas = System.String('c', 1024)
                                  lessonsAndConventions = System.String('d', 1024)
                                  plan = System.String('e', 1024) })

            check
                "mux todo_write wrapper keeps nudge behavior"
                (hasExactHint (str result "output") (hintMethodologyFollowup "first_principles"))
    }

let muxBacklogProjectionSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mux-magic-todo-projection-"
        let reg = sharedMuxRegistration ()
        let tf = muxMessageTransform reg

        if isNullish tf then
            check "mux messagesTransform exposed for magic todo projection" false
        else
            let todoInput (report: string) content status priority =
                createObj
                    [ "ahaMoments", box report
                      "changesAndReasons", box (report + "_changes")
                      "gotchas", box (report + "_gotchas")
                      "lessonsAndConventions", box (report + "_lessons")
                      "plan", box (report + "_plan")
                      "todos",
                      box [| createObj [ "content", box content; "status", box status; "priority", box priority ] |] ]

            let todoOutput count =
                createObj [ "success", box true; "count", box count ]

            let messages =
                [| muxTextMessage "todo-user-1" "user" "plan phase"
                   muxDynamicToolMessage
                       "todo-1"
                       "todo_write"
                       "todo-call-a"
                       (todoInput "planned phase" "Plan change" "in_progress" "high")
                       (todoOutput 1)
                   muxTextMessage "todo-user-2" "user" "implemented phase"
                   muxDynamicToolMessage
                       "todo-2"
                       "todo_write"
                       "todo-call-b"
                       (todoInput "implemented phase" "Implement change" "completed" "high")
                       (todoOutput 1)
                   muxTextMessage "todo-user-3" "user" "verified phase"
                   muxDynamicToolMessage
                       "todo-3"
                       "todo_write"
                       "todo-call-c"
                       (todoInput "verified phase" "Verify change" "completed" "medium")
                       (todoOutput 1) |]

            let todoEvent (report: string) content status priority =
                let parsedStatus =
                    match status with
                    | "in_progress" -> Wanxiangshu.Kernel.ToolArgs.TodoItemStatus.InProgress
                    | "completed" -> Wanxiangshu.Kernel.ToolArgs.TodoItemStatus.Completed
                    | "cancelled" -> Wanxiangshu.Kernel.ToolArgs.TodoItemStatus.Cancelled
                    | _ -> Wanxiangshu.Kernel.ToolArgs.TodoItemStatus.Todo

                let parsedPriority =
                    match priority with
                    | "low" -> Wanxiangshu.Kernel.ToolArgs.TodoItemPriority.Low
                    | "medium" -> Wanxiangshu.Kernel.ToolArgs.TodoItemPriority.Medium
                    | "high" -> Wanxiangshu.Kernel.ToolArgs.TodoItemPriority.High
                    | _ -> Wanxiangshu.Kernel.ToolArgs.TodoItemPriority.Low

                { Wanxiangshu.Runtime.WorkBacklogToolsCodec.TodoWriteArgs.AhaMoments = report
                  Wanxiangshu.Runtime.WorkBacklogToolsCodec.TodoWriteArgs.ChangesAndReasons = report + "_changes"
                  Wanxiangshu.Runtime.WorkBacklogToolsCodec.TodoWriteArgs.Gotchas = report + "_gotchas"
                  Wanxiangshu.Runtime.WorkBacklogToolsCodec.TodoWriteArgs.LessonsAndConventions = report + "_lessons"
                  Wanxiangshu.Runtime.WorkBacklogToolsCodec.TodoWriteArgs.Plan = report + "_plan"
                  Wanxiangshu.Runtime.WorkBacklogToolsCodec.TodoWriteArgs.Todos =
                    [| { Wanxiangshu.Runtime.WorkBacklogToolsCodec.TodoItem.Content = content
                         Wanxiangshu.Runtime.WorkBacklogToolsCodec.TodoItem.Status = parsedStatus
                         Wanxiangshu.Runtime.WorkBacklogToolsCodec.TodoItem.Priority = parsedPriority } |]
                  Wanxiangshu.Runtime.WorkBacklogToolsCodec.TodoWriteArgs.SelectMethodology = [] }

            do!
                Wanxiangshu.Runtime.BacklogEventWriter.appendWorkBacklogCommittedOrFail
                    workspaceDir
                    "mux-magic-todo-session"
                    (todoEvent "planned phase" "Plan change" "in_progress" "high")

            do!
                Wanxiangshu.Runtime.BacklogEventWriter.appendWorkBacklogCommittedOrFail
                    workspaceDir
                    "mux-magic-todo-session"
                    (todoEvent "implemented phase" "Implement change" "completed" "high")

            do!
                Wanxiangshu.Runtime.BacklogEventWriter.appendWorkBacklogCommittedOrFail
                    workspaceDir
                    "mux-magic-todo-session"
                    (todoEvent "verified phase" "Verify change" "completed" "medium")

            let out = createObj [ "messages", box messages ]

            let input =
                createObj
                    [ "agent", box "manager"
                      "sessionID", box "mux-magic-todo-session"
                      "directory", box workspaceDir ]

            do! (tf $ (input, out)) |> unbox<JS.Promise<unit>>
            let transformed = unbox<obj[]> (get out "messages")

            let texts =
                transformed
                |> Array.collect (fun msg ->
                    let parts = unbox<obj[]> (get msg "parts")

                    parts
                    |> Array.choose (fun part ->
                        if str part "type" = "text" then
                            Some(str part "text")
                        else
                            None))

            check
                "mux magic todo projection injects folded backlog text"
                (texts
                 |> Array.exists (fun text ->
                     text.Contains("Completed work from folded turns. File changes are already on disk.")
                     && text.Contains("user_message:")
                     && text.Contains("planned phase")))

            do! rmAsync workspaceDir
    }
