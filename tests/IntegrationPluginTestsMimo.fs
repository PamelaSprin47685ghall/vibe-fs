module Wanxiangshu.Tests.IntegrationPluginTestsMimo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.Dyn

let mimoConfigSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mimo-plugin-config-"
        let! p = Wanxiangshu.Hosts.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |})
        Wanxiangshu.Tests.IntegrationPluginTestsCommon.pluginShape p
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
        check "mimo config build exists" (not (isNullish (get agents "build")))
        check "mimo config plan exists" (not (isNullish (get agents "plan")))
        check "mimo manager tools.glob present" (not (isNullish (get managerTools "glob")))
        check "mimo manager tools.skill present" (not (isNullish (get managerTools "skill")))
        let coder = get agents "coder"
        check "mimo config coder exists" (not (isNullish coder))
        let coderTools = get coder "tools"
        check "mimo config coder tools.question false" (not (unbox<bool> (get coderTools "question")))
        check "mimo config coder tools.glob present" (not (isNullish (get coderTools "glob")))
        check "mimo config coder tools.skill present" (not (isNullish (get coderTools "skill")))
        do! rmAsync workspaceDir
    }

let mimoTuiTodoFallbackSpec () =
    promise {
        let sessionID = "mimo-tui-session"
        let mutable disposeHook: (unit -> unit) option = None
        let routeCurrent = createObj [ "name", box "home" ]

        let recoveredTodos =
            [| createObj
                   [ "content", box "Ship sidebar sync"
                     "status", box "in_progress"
                     "priority", box "high" ] |]

        let taskPart =
            box (
                createObj
                    [ "type", box "tool"
                      "tool", box "task"
                      "state",
                      box (
                          createObj
                              [ "status", box "completed"
                                "input", box (createObj [ "todos", box recoveredTodos ]) ]
                      ) ]
            )

        let api =
            createObj
                [ "state",
                  box (
                      createObj
                          [ "session",
                            box (
                                createObj
                                    [ "todo", box (System.Func<string, obj>(fun _ -> box [||]))
                                      "messages",
                                      box (
                                          System.Func<string, obj>(fun sid ->
                                              if sid = sessionID then
                                                  box [| box (createObj [ "id", box "msg-1" ]) |]
                                              else
                                                  box [||])
                                      ) ]
                            )
                            "part",
                            box (
                                System.Func<string, obj>(fun messageID ->
                                    if messageID = "msg-1" then box [| taskPart |] else box [||])
                            ) ]
                  )
                  "lifecycle",
                  box (
                      createObj
                          [ "onDispose",
                            box (
                                System.Func<obj, obj>(fun fn ->
                                    disposeHook <- Some(unbox<unit -> unit> fn)
                                    box (fun () -> ()))
                            ) ]
                  )
                  "command", box (createObj [ "register", box (System.Func<obj, obj>(fun _ -> box (fun () -> ()))) ])
                  "route", box (createObj [ "current", box routeCurrent ]) ]

        let pluginObj = Wanxiangshu.Hosts.Opencode.PluginMimoTui.plugin
        do! (get pluginObj "tui") $ (api, null, null) |> unbox<JS.Promise<unit>>

        let todosAfterInstall =
            call1 (get (get (get api "state") "session") "todo") (box sessionID)
            |> unbox<obj array>

        check "mimo tui todo fallback recovers task todos" (todosAfterInstall.Length = 1)
        check "mimo tui todo fallback preserves content" (str todosAfterInstall.[0] "content" = "Ship sidebar sync")
        check "mimo tui todo fallback preserves status" (str todosAfterInstall.[0] "status" = "in_progress")
        disposeHook |> Option.iter (fun dispose -> dispose ())

        let todosAfterDispose =
            call1 (get (get (get api "state") "session") "todo") (box sessionID)
            |> unbox<obj array>

        check "mimo tui todo fallback restores original todo getter on dispose" (todosAfterDispose.Length = 0)
    }
