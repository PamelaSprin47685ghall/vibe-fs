module Wanxiangshu.Tests.IntegrationPluginTestsCommon

open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.Dyn

let pluginShape (p: obj) =
    check "plugin.name" (str p "name" = "wanxiangshu")
    check "plugin.tool" (typeIs (get p "tool") "object")
    check "plugin.config" (typeIs (get p "config") "function")
    check "plugin.event" (typeIs (get p "event") "function")
    check "plugin.mcp" (typeIs (get p "mcp") "object")
    check "plugin.tool.execute.after" (typeIs (get p "tool.execute.after") "function")

    check
        "plugin.experimental.chat.messages.transform"
        (typeIs (get p "experimental.chat.messages.transform") "function")

    check "plugin.experimental.chat.system.transform" (typeIs (get p "experimental.chat.system.transform") "function")
    check "plugin.command.execute.before" (typeIs (get p "command.execute.before") "function")

let registrationShape (reg: obj) =
    check "mux.toolNames" (isArray (get reg "toolNames"))
    check "mux.tools" (isArray (get reg "tools"))
    check "mux.mcpServers" (typeIs (get reg "mcpServers") "object")
    check "mux.tool.execute.after" (typeIs (get reg "tool.execute.after") "function")
    let policy = (get reg "getToolPolicy") $ ("x", "manager")
    check "mux.getToolPolicy non-null" (not (isNullish policy) && typeIs policy "object")
    let removes = unbox<string[]> (get policy "remove")
    check "mux.getToolPolicy manager removes write" (removes |> Array.contains "write")
    let coderPolicy = (get reg "getToolPolicy") $ ("x", "coder")
    let coderRemoves = unbox<string[]> (get coderPolicy "remove")
    check "mux.getToolPolicy coder keeps write" (not (coderRemoves |> Array.contains "write"))
