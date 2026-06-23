module VibeFs.Tests.IntegrationPluginTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Kernel.Dyn
open VibeFs.Mux.Plugin
open VibeFs.Opencode.Plugin
open VibeFs.Shell.TreeSitterShell
open VibeFs.Kernel.TreeSitterKernel

let pluginShape (p: obj) =
    check "plugin.name" (str p "name" = "kunwei")
    check "plugin.tool" (typeIs (get p "tool") "object")
    check "plugin.config" (typeIs (get p "config") "function")
    check "plugin.event" (typeIs (get p "event") "function")
    check "plugin.mcp" (typeIs (get p "mcp") "object")
    check "plugin.tool.execute.after" (typeIs (get p "tool.execute.after") "function")
    check "plugin.experimental.chat.messages.transform" (typeIs (get p "experimental.chat.messages.transform") "function")
    check "plugin.command.execute.before" (typeIs (get p "command.execute.before") "function")

let registrationShape (reg: obj) =
    check "mux.toolNames" (isArray (get reg "toolNames"))
    check "mux.tools" (isArray (get reg "tools"))
    check "mux.mcpServers" (typeIs (get reg "mcpServers") "object")
    check "mux.contextInjector" (typeIs (get reg "contextInjector") "object")
    let policy = (get reg "getToolPolicy") $ ("x", "manager")
    check "mux.getToolPolicy non-null" (not (isNullish policy) && typeIs policy "object")
    let removes = unbox<string[]> (get policy "remove")
    check "mux.getToolPolicy manager removes write" (removes |> Array.contains "write")
    let coderPolicy = (get reg "getToolPolicy") $ ("x", "coder")
    let coderRemoves = unbox<string[]> (get coderPolicy "remove")
    check "mux.getToolPolicy coder keeps write" (not (coderRemoves |> Array.contains "write"))

let syntaxSpec () = promise {
    let! result = checkSyntax "const x = 1;" "test.js"
    match result with
    | Ok (_, errors) -> check "tree-sitter no errors" (errors.Length = 0)
    | Failed _ -> check "tree-sitter not failed" false
}

let webfetchSchemaSpec (reg: obj) =
    let tools = unbox<obj[]> (get reg "tools")
    let wf = tools |> Array.find (fun t -> str t "name" = "webfetch")
    let props = get (get wf "parameters") "properties"
    check "webfetch schema has url" (not (isNullish (get props "url")))
    check "webfetch schema has extract_main" (not (isNullish (get props "extract_main")))
    check "webfetch schema has timeout" (not (isNullish (get props "timeout")))
    check "webfetch execute is function" (typeIs (get wf "execute") "function")

let slashCommandsSpec (reg: obj) =
    let cmds = unbox<obj[]> (get reg "slashCommands")
    check "slash commands count" (cmds.Length = 2)
    let loopCmd = cmds |> Array.find (fun c -> str c "key" = "loop")
    check "loop command has execute" (typeIs (get loopCmd "execute") "function")

let countsSpec (reg: obj) =
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let tools = unbox<obj[]> (get reg "tools")
    let names = tools |> Array.map (fun t -> str t "name")
    check "wrapper count" (wrappers.Length = 5)
    check "tool count" (tools.Length = 16)
    check "mux has knowledge_graph_fetch tool" (names |> Array.contains "knowledge_graph_fetch")
    check "mux has return_bookkeeper tool" (names |> Array.contains "return_bookkeeper")
    check "mux has return_reviewer tool" (names |> Array.contains "return_reviewer")
    check "mux has select_methodology tool" (names |> Array.contains "select_methodology")

let configSpec () = promise {
    let! workspaceDir = mkdtempAsync "plugin-config-"
    let! p = plugin (box {| directory = workspaceDir |})
    let! cfg = (get p "config") $ (createObj []) |> unbox<JS.Promise<obj>>
    let agents = get cfg "agent"
    check "config manager exists" (not (isNullish (get agents "manager")))
    check "config build exists" (not (isNullish (get agents "build")))
    check "config plan exists" (not (isNullish (get agents "plan")))
    let manager = get agents "manager"
    check "config manager mode primary" (str manager "mode" = "primary")
    let tools = get manager "tools"
    check "config manager tools.bash false" (unbox<bool> (get tools "bash") = false)
    check "config manager tools.glob present" (not (isNullish (get tools "glob")))
    check "config manager tools.skill present" (not (isNullish (get tools "skill")))
    let coder = get agents "coder"
    let coderTools = get coder "tools"
    check "config coder tools.question false" (not (unbox<bool> (get coderTools "question")))
    check "config coder tools.submit_review false" (not (unbox<bool> (get coderTools "submit_review")))
    check "config coder tools.glob present" (not (isNullish (get coderTools "glob")))
    check "config coder tools.skill present" (not (isNullish (get coderTools "skill")))
    let permission = get manager "permission"
    check "config manager permission.bash deny" (str permission "bash" = "deny")
    check "config manager permission.glob present" (not (isNullish (get permission "glob")))
    check "config manager permission.skill present" (not (isNullish (get permission "skill")))
    do! rmAsync workspaceDir
}

let mimoConfigSpec () = promise {
    let! workspaceDir = mkdtempAsync "mimo-plugin-config-"
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |})
    pluginShape p
    let! cfg = (get p "config") $ (createObj []) |> unbox<JS.Promise<obj>>
    let agents = get cfg "agent"
    let manager = get agents "manager"
    let managerPermissions = get manager "permission"
    check "mimo manager permission.task allow" (str managerPermissions "task" = "allow")
    check "mimo manager permission.actor deny" (str managerPermissions "actor" = "deny")
    check "mimo manager permission.workflow deny" (str managerPermissions "workflow" = "deny")
    let managerTools = get manager "tools"
    check "mimo manager tools.task present" (not (isNullish (get managerTools "task")))
    check "mimo manager tools.actor false" (unbox<bool> (get managerTools "actor") = false)
    check "mimo manager tools.workflow false" (unbox<bool> (get managerTools "workflow") = false)
    let taskParams =
        createObj [
            "type", box "object"
            "properties", box (createObj [ "todos", box (createObj [ "type", box "array" ]) ])
            "required", box [| box "todos" |]
            "additionalProperties", box false
        ]
    let todoDef = createObj [ "description", box "old desc"; "parameters", box taskParams ]
    do! (get p "tool.definition") $ (createObj [ "toolID", box "task" ], todoDef) |> unbox<JS.Promise<unit>>
    check "mimo tool.definition rewrites task description" (str todoDef "description" |> fun text -> text.Contains("append-only work backlog") && text.Contains("full todos list"))
    check "mimo tool.definition merges completedWorkReport into parameters" (not (isNullish (get (get (get todoDef "parameters") "properties") "completedWorkReport")))
    check "mimo tool.definition keeps todos schema" (not (isNullish (get (get (get todoDef "parameters") "properties") "todos")))

    let sessionID = "mimo-session-1"
    let makeTaskMessage id report =
        box (createObj [
            "info", box (createObj [
                "id", box id
                "sessionID", box sessionID
                "role", box "toolResult"
                "time", box (createObj [ "created", box 0 ])
                "agent", box "orchestrator"
                "model", box (createObj [ "providerID", box ""; "modelID", box "" ])
            ])
            "parts", box [|
                box (createObj [
                    "type", box "tool"
                    "tool", box "task"
                    "state", box (createObj [
                        "status", box "completed"
                        "input", box (createObj [
                            "todos", box [| createObj [ "content", box report; "status", box "completed"; "priority", box "high" ] |]
                            "completedWorkReport", box report
                        ])
                        "output", box report
                    ])
                ])
            |]
        ])
    let makeUserMessage id text =
        box (createObj [
            "info", box (createObj [
                "id", box id
                "sessionID", box sessionID
                "role", box "user"
                "time", box (createObj [ "created", box 0 ])
                "agent", box "orchestrator"
                "model", box (createObj [ "providerID", box ""; "modelID", box "" ])
            ])
            "parts", box [| box (createObj [ "type", box "text"; "text", box text ]) |]
        ])

    let messages = [|
        makeTaskMessage "mimo-msg-1" "First report from the first task."
        makeUserMessage "mimo-user-1" "Fold this user note into the summary."
        makeTaskMessage "mimo-msg-2" "Second report from the second task."
        makeUserMessage "mimo-user-2" "Keep this user detail in the projection."
        makeTaskMessage "mimo-msg-3" "Third report from the final task."
    |]
    let output = createObj [ "messages", box messages ]
    do! (get p "experimental.chat.messages.transform") $ (createObj [ "agent", box "manager" ], output) |> unbox<JS.Promise<unit>>
    let transformedMessages = unbox<obj[]> (get output "messages")
    let prefixMessages = transformedMessages |> Array.filter (fun message -> str (get message "info") "id" |> fun id -> id.StartsWith("magic-todo-prefix-"))
    check "mimo messages.transform emits folded prefix messages" (prefixMessages.Length = 2)
    check "mimo messages.transform prefix keeps folded user note" (
        prefixMessages
        |> Array.exists (fun message ->
            let parts = unbox<obj[]> (get message "parts")
            str parts.[0] "text" |> fun text -> text.Contains("Fold this user note"))
    )
    let projectedMessage = transformedMessages |> Array.find (fun message -> str (get message "info") "id" = "mimo-msg-1")
    let projectedParts = unbox<obj[]> (get projectedMessage "parts")
    let projectedOutput = str (get projectedParts.[0] "state") "output"
    check "mimo messages.transform projects backlog content" (
        projectedOutput.Contains("First report from the first task.")
        && projectedOutput.Contains("Second report from the second task.")
        && projectedOutput.Contains("Fold this user note into the summary.")
    )

    let compactingContext = [||]
    let compactingOutput = createObj [ "context", box compactingContext ]
    do! (get p "experimental.session.compacting") $ (createObj [ "sessionID", box sessionID ], compactingOutput) |> unbox<JS.Promise<unit>>
    let compactingContextAfter = unbox<obj[]> (get compactingOutput "context")
    check "mimo session.compacting leaves context untouched" (compactingContextAfter.Length = 0)
    do! rmAsync workspaceDir
}

let mimoTuiTodoFallbackSpec () = promise {
    let sessionID = "mimo-tui-session"
    let mutable disposeHook : (unit -> unit) option = None
    let routeCurrent = createObj [ "name", box "home" ]
    let recoveredTodos =
        [| createObj [
            "content", box "Ship sidebar sync"
            "status", box "in_progress"
            "priority", box "high"
        ] |]
    let taskPart =
        box (createObj [
            "type", box "tool"
            "tool", box "task"
            "state", box (createObj [
                "status", box "completed"
                "input", box (createObj [ "todos", box recoveredTodos ])
            ])
        ])
    let api =
        createObj [
            "state", box (createObj [
                "session", box (createObj [
                    "todo", box (System.Func<string, obj>(fun _ -> box [||]))
                    "messages", box (System.Func<string, obj>(fun sid ->
                        if sid = sessionID then
                            box [| box (createObj [ "id", box "msg-1" ]) |]
                        else box [||]))
                ])
                "part", box (System.Func<string, obj>(fun messageID ->
                    if messageID = "msg-1" then box [| taskPart |] else box [||]))
            ])
            "lifecycle", box (createObj [
                "onDispose", box (System.Func<obj, obj>(fun fn ->
                    disposeHook <- Some (unbox<unit -> unit> fn)
                    box (fun () -> ())))
            ])
            "command", box (createObj [ "register", box (System.Func<obj, obj>(fun _ -> box (fun () -> ()))) ])
            "route", box (createObj [ "current", box routeCurrent ])
        ]
    let pluginObj = VibeFs.Opencode.PluginMimoTui.plugin
    do! (get pluginObj "tui") $ (api, null, null) |> unbox<JS.Promise<unit>>
    let todosAfterInstall = call1 (get (get (get api "state") "session") "todo") (box sessionID) |> unbox<obj array>
    check "mimo tui todo fallback recovers task todos" (todosAfterInstall.Length = 1)
    check "mimo tui todo fallback preserves content" (str todosAfterInstall.[0] "content" = "Ship sidebar sync")
    check "mimo tui todo fallback preserves status" (str todosAfterInstall.[0] "status" = "in_progress")
    disposeHook |> Option.iter (fun dispose -> dispose ())
    let todosAfterDispose = call1 (get (get (get api "state") "session") "todo") (box sessionID) |> unbox<obj array>
    check "mimo tui todo fallback restores original todo getter on dispose" (todosAfterDispose.Length = 0)
}

let topLevelExportsSpec () =
    check "top-level getPluginToolPolicy is function" (typeIs getPluginToolPolicy "function")
    check "top-level collectReadOutputs is function" (typeIs collectReadOutputs "function")
    check "top-level deduplicateReadOutputsWithSeen is function" (typeIs deduplicateReadOutputsWithSeen "function")

let run () : JS.Promise<unit> =
    promise {
        let! workspaceDir = mkdtempAsync "plugin-run-"
        let! p = plugin (box {| directory = workspaceDir |})
        pluginShape p
        let reg = createRegistration (createObj [])
        registrationShape reg
        topLevelExportsSpec ()
        do! syntaxSpec ()
        webfetchSchemaSpec reg
        slashCommandsSpec reg
        countsSpec reg
        do! configSpec ()
        do! mimoConfigSpec ()
        do! mimoTuiTodoFallbackSpec ()
        do! rmAsync workspaceDir
    }
