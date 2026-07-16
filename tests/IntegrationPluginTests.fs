module Wanxiangshu.Tests.IntegrationPluginTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationPluginTestsCommon
open Wanxiangshu.Tests.IntegrationPluginTestsMimo
open Wanxiangshu.Hosts.Mux.Plugin
open Wanxiangshu.Hosts.Opencode.Plugin
open Wanxiangshu.Runtime.TreeSitterShell
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Runtime.Dyn

let syntaxSpec () =
    promise {
        let! result = checkSyntax "const x = 1;" "test.js"

        match result with
        | Ok(_, errors) -> check "tree-sitter no errors" (errors.Length = 0)
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
    equal "tool count" 14 tools.Length
    check "mux has meditator tool" (names |> Array.contains "meditator")
    check "mux has submit_review tool" (names |> Array.contains "submit_review")
    check "mux does not expose return_reviewer tool" (not (names |> Array.contains "return_reviewer"))

let configSpec () =
    promise {
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

let topLevelExportsSpec () =
    check "top-level getPluginToolPolicy is function" (typeIs getPluginToolPolicy "function")

let systemTransformSpec (p: obj) =
    promise {
        let tf = get p "experimental.chat.system.transform"
        let systemArray = [| "baseline system prompt"; "extra prompt" |]
        let output = createObj [ "system", box systemArray ]
        do! tf $ (createObj [], output) |> unbox<JS.Promise<unit>>
        let systemAfter = get output "system" |> unbox<obj array>

        check
            "system transform mutates array in-place (same reference)"
            (obj.ReferenceEquals(systemArray, box systemAfter))

        check "system transform replaces with single-element array" (systemAfter.Length = 1)
        let head = systemAfter.[0] :?> string
        check "system transform first element is not baseline" (head <> "baseline system prompt")
        check "system transform first element is not extra" (head <> "extra prompt")
        check "system transform first element is not empty" (head <> "")
    }

let executorSchemaSpec (p: obj) =
    let toolObj = get (get p "tool") "executor"
    check "plugin.tool.executor exists" (not (isNullish toolObj))
    let args = get toolObj "args"
    check "executor zod schema has max_bytes" (not (isNullish (get args "max_bytes")))

let run () : JS.Promise<unit> =
    promise {
        let! workspaceDir = mkdtempAsync "plugin-run-"
        let! p = plugin (box {| directory = workspaceDir |})
        pluginShape p
        executorSchemaSpec p
        do! systemTransformSpec p
        let reg = Wanxiangshu.Tests.IntegrationToolSetup.sharedMuxRegistration ()
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
