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
open Wanxiangshu.Hosts.Mux.CompactionTransform
open Wanxiangshu.Hosts.Mux.MessageTransform

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

    createObj
        [ "toolNames", box muxToolNames
          "tools", box tools
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
