module VibeFs.Tests.IntegrationPluginTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Kernel.Dyn
open VibeFs.Index
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

let policySpec () =
    let pol1 = getPluginToolPolicy "some-agent" "manager"
    let removes = unbox<string[]> (get pol1 "remove")
    check "getPluginToolPolicy manager removes write" (removes |> Array.contains "write")
    let pol2 = getPluginToolPolicy "some-agent"
    check "getPluginToolPolicy without role returns policy" (not (isNullish pol2))
    let pol3 = getPluginToolPolicy "some-agent" "coder"
    check "getPluginToolPolicy coder keeps write" (not ((unbox<string[]> (get pol3 "remove")) |> Array.contains "write"))

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

let run () : JS.Promise<unit> =
    async {
        let! workspaceDir = mkdtempAsync "plugin-run-" |> Async.AwaitPromise
        let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
        pluginShape p
        let reg = createRegistration (createObj [])
        registrationShape reg
        policySpec ()
        do! syntaxSpec ()
        webfetchSchemaSpec reg
        slashCommandsSpec reg
        countsSpec reg
        do! configSpec ()
        do! rmAsync workspaceDir |> Async.AwaitPromise
    }
    |> Async.StartAsPromise
