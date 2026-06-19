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

let syntaxSpec () = async {
    let! result = checkSyntax "const x = 1;" "test.js" |> Async.AwaitPromise
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
    check "wrapper count" (wrappers.Length = 7)
    check "tool count" (tools.Length = 12)

let configSpec () = async {
    let! workspaceDir = mkdtempAsync "plugin-config-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let! cfg = (get p "config") $ (createObj []) |> unbox<JS.Promise<obj>> |> Async.AwaitPromise
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
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let mimoConfigSpec () = async {
    let! workspaceDir = mkdtempAsync "mimo-plugin-config-" |> Async.AwaitPromise
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    pluginShape p
    let! cfg = (get p "config") $ (createObj []) |> unbox<JS.Promise<obj>> |> Async.AwaitPromise
    let agents = get cfg "agent"
    let manager = get agents "manager"
    let managerPermissions = get manager "permission"
    check "mimo manager permission.task allow" (str managerPermissions "task" = "allow")
    check "mimo manager permission.actor deny" (str managerPermissions "actor" = "deny")
    let managerTools = get manager "tools"
    check "mimo manager tools.task present" (not (isNullish (get managerTools "task")))
    check "mimo manager tools.actor false" (unbox<bool> (get managerTools "actor") = false)
    let taskParams =
        createObj [
            "type", box "object"
            "properties", box (createObj [ "operation", box (createObj [ "type", box "object" ]) ])
            "required", box [| box "operation" |]
            "additionalProperties", box false
        ]
    let todoDef = createObj [ "description", box "old desc"; "parameters", box taskParams ]
    do! (get p "tool.definition") $ (createObj [ "toolID", box "task" ], todoDef) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "mimo tool.definition rewrites task description" (str todoDef "description" |> fun text -> text.Contains("append-only work backlog") && text.Contains("operation"))
    check "mimo tool.definition merges completedWorkReport into parameters" (not (isNullish (get (get (get todoDef "parameters") "properties") "completedWorkReport")))
    check "mimo tool.definition removes host task_id from schema" (isNullish (get (get (get todoDef "parameters") "properties") "task_id"))
    check "mimo tool.definition leaves task operation schema" (not (isNullish (get (get (get todoDef "parameters") "properties") "operation")))

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
                            "operation", box (createObj [ "action", box "done"; "id", box "T1"; "event_summary", box report ])
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
    do! (get p "experimental.chat.messages.transform") $ (createObj [ "agent", box "manager" ], output) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
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
    do! (get p "experimental.session.compacting") $ (createObj [ "sessionID", box sessionID ], compactingOutput) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let compactingContextAfter = unbox<obj[]> (get compactingOutput "context")
    check "mimo session.compacting uses task naming" (
        compactingContextAfter.Length = 1
        && (string compactingContextAfter.[0]).Contains("task")
        && not ((string compactingContextAfter.[0]).Contains("todowrite"))
    )
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let run () : JS.Promise<unit> =
    async {
        let! workspaceDir = mkdtempAsync "plugin-run-" |> Async.AwaitPromise
        let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
        pluginShape p
        let reg = createRegistration (createObj [])
        registrationShape reg
        do! syntaxSpec ()
        webfetchSchemaSpec reg
        slashCommandsSpec reg
        countsSpec reg
        do! configSpec ()
        do! mimoConfigSpec ()
        do! rmAsync workspaceDir |> Async.AwaitPromise
    }
    |> Async.StartAsPromise
