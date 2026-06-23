module VibeFs.Tests.IntegrationMuxToolSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Tests.IntegrationMuxSetup

open VibeFs.Kernel.Executor
open VibeFs.Kernel.HostTools
open VibeFs.Mux.Plugin
open VibeFs.Shell.MagicSessionStore
open VibeFs.Shell.Dyn


let muxTodoWriteWrapperSchemaSpec () = promise {
    let reg = createRegistration (createObj [])
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
    let reg = createRegistration (createObj [])
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
        check "mux todo_write wrapper captures completedWorkReport" (tryGetReport opencode "todo-call-1" = Some "finished wrapper capture")
        check "mux todo_write wrapper keeps nudge behavior" ((str result "nudge").Contains "meditator")
}

let muxMagicTodoProjectionSpec () = promise {
    let reg = createRegistration (createObj [])
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

let muxExecutorRoCatPrependsWarningSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-executor-ro-warning-"
    let reg = createRegistration (createObj [])
    let executor = muxToolByName reg "executor"
    if isNullish executor then
        check "mux registration exposes executor tool for ro warning" false
    else
        let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box "mux-executor-ro-warning"; "sessionID", box "mux-executor-ro-warning" ]
        let args = createObj [ "language", box "shell"; "program", box "cat /etc/passwd"; "timeout_type", box "short"; "mode", box "ro" ]
        let! result = ((get executor "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        check "mux executor ro cat does not prepend warning" (not (result.StartsWith readOnlyWarning))
    do! rmAsync workspaceDir
}

let muxMeditatorReadsFilesFromCwdSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-meditator-files-"
    let filePath = pathModule?join(workspaceDir, "note.md")
    do! writeFileAsync filePath "deep context"
    let reg = createRegistration (minimalMuxDeps ())
    let meditator = muxToolByName reg "meditator"
    if isNullish meditator then
        check "mux registration exposes meditator tool" false
    else
        let prompts = ResizeArray<string>()
        let taskService =
            createObj
                [ "create",
                  box (System.Func<obj, JS.Promise<obj>>(fun input ->
                      promise {
                          prompts.Add(str input "prompt")
                          return box {| success = true; data = box {| taskId = "meditator-task-1"; kind = "agent" |} |}
                      }))
                  "waitForAgentReport",
                  box (System.Func<string, obj, JS.Promise<obj>>(fun _ _ ->
                      Promise.lift (box {| reportMarkdown = "meditated" |}))) ]
        let ctx = createObj [ "cwd", box workspaceDir; "workspaceId", box "mux-meditator-files"; "taskService", box taskService ]
        let args = createObj [ "intent", box "reason"; "files", box [| "note.md" |] ]
        let! result = ((get meditator "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        let promptText = if prompts.Count > 0 then prompts.[0] else ""
        check "mux meditator returns subagent report" (result = "meditated")
        check "mux meditator prompt keeps requested relative file path" (promptText.Contains "note.md")
        check "mux meditator prompt includes file content" (promptText.Contains "deep context")
        check "mux meditator prompt does not mark readable file skipped" (not (promptText.Contains "(skipped)"))
    do! rmAsync workspaceDir
}

let muxExecutorModeSchemaSpec () = promise {
    let reg = createRegistration (createObj [])
    let modeSchema = muxExecutorModeSchema reg
    check "mux executor mode schema exists" (not (isNullish modeSchema))
    check "mux executor mode schema enum ro/rw" (enumValues modeSchema = [| "ro"; "rw" |])
    let executor = muxToolByName reg "executor"
    let required = muxToolSchemaRequired executor
    check "mux executor mode is required" (required |> Array.contains "mode")
}

let muxReadToolReturnsContentSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-read-content-"
    let filePath = pathModule?join(workspaceDir, "sample.txt")
    let fileContent = "line1\nline2\nline3\nline4\nline5"
    do! writeFileAsync filePath fileContent
    let reg = createRegistration (createObj [])
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let fileReadWrapper = wrappers |> Array.tryFind (fun w -> str w "targetTool" = "file_read")
    if isNullish fileReadWrapper then
        check "mux registration exposes file_read wrapper" false
    else
        let fakeHostRead =
            box {| execute =
                    System.Func<obj, obj, JS.Promise<obj>>(fun args _config ->
                        promise {
                            let path = str args "path"
                            if path = filePath then
                                return box {| success = true; content = fileContent |}
                            else
                                return box {| success = false; error = "file not found" |}
                        }) |}
        (get fileReadWrapper "wrapper") $ (fakeHostRead, createObj []) |> ignore
        let readTool = muxToolByName reg "read"
        if isNullish readTool then
            check "mux registration exposes read tool" false
        else
            let ctx = createObj [ "directory", box workspaceDir; "sessionID", box "mux-read-content-session" ]
            let args = createObj [ "path", box filePath ]
            let! result = ((get readTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
            check "mux read returns non-nullish content" (not (isNullish result))
            check "mux read returns expected line text" (result.Contains "line1" && result.Contains "line5")
            check "mux read does not stringify undefined" (result <> "undefined")
            check "mux read does not stringify object" (not (result.Contains "[object Object]"))
    do! rmAsync workspaceDir
}

let muxReadToolListsDirectoriesSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-read-directory-"
    do! writeFileAsync (pathModule?join(workspaceDir, "note.txt")) "alpha\nbeta"
    let reg = createRegistration (createObj [])
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let fileReadWrapper = wrappers |> Array.tryFind (fun w -> str w "targetTool" = "file_read")
    if isNullish fileReadWrapper then
        check "mux registration exposes file_read wrapper for directory read" false
    else
        let fakeHostRead =
            box {| execute =
                    System.Func<obj, obj, JS.Promise<obj>>(fun args _config ->
                        promise {
                            let path = str args "path"
                            if path = workspaceDir then
                                return box {| success = false; error = $"Path is a directory, not a file: {workspaceDir}" |}
                            else
                                return box {| success = true; content = "1\talpha\n2\tbeta" |}
                        }) |}
        (get fileReadWrapper "wrapper") $ (fakeHostRead, createObj []) |> ignore
        let readTool = muxToolByName reg "read"
        if isNullish readTool then
            check "mux registration exposes read tool for directory read" false
        else
            let ctx = createObj [ "directory", box workspaceDir; "sessionID", box "mux-read-directory-session" ]
            let args = createObj [ "path", box workspaceDir ]
            let! result = ((get readTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
            check "mux read returns directory listing" (result.Contains "note.txt" && result.Contains "total 1")
            check "mux read directory does not return file-only error" (not (result.Contains "Path is a directory, not a file"))
    do! rmAsync workspaceDir
}
