module Wanxiangshu.Hosts.Opencode.PluginMimoTui

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Hosts.Opencode.PluginMimoTuiTodo
open Wanxiangshu.Hosts.Opencode.PluginMimoTuiSubagents

let private tuiImpl (api: obj) : JS.Promise<unit> =
    promise {
        installTodoFallback api
        registerCommands api
    }

[<ExportDefault>]
let plugin =
    box
        {| id = "vibe-fs-mimo-tui"
           tui = tuiImpl |}
