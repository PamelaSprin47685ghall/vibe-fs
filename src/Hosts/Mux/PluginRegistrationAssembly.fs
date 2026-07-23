module Wanxiangshu.Hosts.Mux.PluginRegistrationAssembly

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.HostCapability
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Hosts.Mux.PluginCatalog
open Wanxiangshu.Hosts.Mux.Wrappers
open Wanxiangshu.Hosts.Mux.MessageTransform

let private decorateTool (toolID: string) (target: obj) =
    if not (isNullish target) then
        let mutable properties = Dyn.get target "properties"

        if isNullish properties then
            properties <- createObj []
            Dyn.setKey target "properties" properties

        let addField (key: string) (desc: string) =
            if isNullish (Dyn.get properties key) then
                let propObj = createObj [ "type", box "string"; "description", box desc ]
                Dyn.setKey properties key propObj

        let hasTdd =
            [| "coder"
               "executor"
               "write"
               "edit"
               "pty_spawn"
               "pty_write"
               "pty_read"
               "pty_list"
               "pty_kill" |]
            |> Array.contains toolID

        let hasImpossible =
            [| "executor"; "pty_spawn"; "pty_write"; "pty_read"; "pty_list"; "pty_kill" |]
            |> Array.contains toolID

        let hasNotSuitable =
            [| "coder"; "inspector"; "meditator"; "browser" |] |> Array.contains toolID

        if hasTdd then
            addField
                "follow-tdd-and-kolmogorov-principles"
                "MUST acknowledge that you have followed TDD and Kolmogorov principles and kept todo updated"

        if hasImpossible then
            addField
                "impossible-via-other-tools"
                "MUST acknowledge that this task cannot be done with other tools and only run tests when static analysis cannot handle it"

        if hasNotSuitable then
            addField
                "not-suitable-via-continue-tool"
                "MUST acknowledge that this task cannot be completed using the continue tool"

let assembleRegistrationObject
    (deps: obj)
    (scope: RuntimeScope)
    (tools: ToolDefinition array)
    (wrappers: obj)
    (mcpServers: obj)
    (eventHook: obj)
    (slashCommands: obj)
    (messagesTransform: obj)
    (compactingTransform: obj)
    (getToolPolicy: obj)
    : obj =
    let directory = if Dyn.isNullish deps then "" else Dyn.str deps "directory"
    let muxCapabilities: obj = toStringArray muxDefault |> box

    let registeredTools =
        tools
        |> Array.map (fun t ->
            let origParams = box t.parameters
            let origProps = Dyn.get origParams "properties"
            let newProps = createObj []

            if not (isNullish origProps) then
                let keys = emitJsExpr origProps "Object.keys($0)" |> unbox<string array>

                for key in keys do
                    Dyn.setKey newProps key (Dyn.get origProps key)

            let newParams = createObj [ "type", box "object"; "properties", box newProps ]
            let req = Dyn.get origParams "required"

            if not (isNullish req) then
                Dyn.setKey newParams "required" req

            let tCopy =
                createObj
                    [ "name", box t.name
                      "description", box t.description
                      "parameters", box newParams
                      "execute",
                      box (System.Func<obj, obj, JS.Promise<string>>(fun config args -> t.execute config args)) ]

            decorateTool t.name newParams
            tCopy)

    createObj
        [ "toolNames", box muxToolNames
          "tools", box registeredTools
          "wrappers", box wrappers
          "mcpServers", box mcpServers
          "eventHook", box eventHook
          "slashCommands", box slashCommands
          "messagesTransform", box messagesTransform
          "compactingTransform", box compactingTransform
          "getToolPolicy", box getToolPolicy
          "capabilities", muxCapabilities
          "tool.execute.after",
          box (
              System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
                  Wanxiangshu.Hosts.Mux.PluginCatalog.toolExecuteAfter scope input output)
          )
          "tool.execute.before",
          box (System.Func<obj, obj, JS.Promise<unit>>(fun input output -> toolExecuteBefore input output))
          "systemTransform",
          box (System.Func<obj, obj, JS.Promise<unit>>(fun input output -> systemTransform directory input output)) ]
