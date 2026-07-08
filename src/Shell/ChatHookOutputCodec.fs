module Wanxiangshu.Shell.ChatHookOutputCodec

open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeAgentConfigCodec

let chatMessageFromHookOutput (output: obj) : obj option =
    let message = Dyn.get output "message"
    if Dyn.isNullish message then None else Some message

let chatMessageToolsFromOutput (output: obj) : ToolsOverrides option =
    match chatMessageFromHookOutput output with
    | None -> None
    | Some message ->
        let tools = Dyn.get message "tools"
        toolsMapFromObj tools

let filterChatToolsForAgent (host: Host) (agent: string) (tools: ToolsOverrides option) : ToolsOverrides option =
    match tools with
    | None -> None
    | Some m ->
        m
        |> Map.map (fun name enabled -> if canUseForHost host agent name then enabled else false)
        |> Some

let encodeToolsOverridesToMessage (tools: ToolsOverrides) : obj = toolsMapToObj tools
