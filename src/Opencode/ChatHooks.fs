module VibeFs.Opencode.ChatHooks

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.Config
open VibeFs.Kernel.HostTools
open VibeFs.Opencode.AgentConfig
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.Dyn

let private resolveAgentFromMessage (registry: ChildAgentRegistry) (message: obj) : string option =
    if isNullish message then None
    else
        let info = get message "info"
        if isNullish info then None
        else
            let agent = str info "agent"
            if agent <> "" then Some agent
            else
                let sessionID = str info "sessionID"
                if sessionID = "" then None else registry.LookupChildAgent(sessionID)

let private resolveAgent (registry: ChildAgentRegistry) (input: obj) (output: obj) : string =
    let explicit = Dyn.str input "agent"
    if explicit <> "" then explicit
    else
        match registry.LookupChildAgent(Dyn.str input "sessionID") with
        | Some a -> a
        | None -> resolveAgentFromMessage registry (Dyn.get output "message") |> Option.defaultValue "manager"

let private resolveChatTools (host: Host) (agent: string) (existingTools: obj) : obj =
    let next = createObj []
    if not (Dyn.isNullish existingTools) then
        for key in Dyn.keys existingTools do
            if canUseForHost host agent key then
                setKey next key (Dyn.get existingTools key)
            else
                setKey next key (box false)
    next

let chatMessageFor (host: Host) (registry: ChildAgentRegistry) (nudgeHook: VibeFs.Opencode.NudgeHook.NudgeHook) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let agent = resolveAgent registry input output
        let sessionID = VibeFs.Kernel.Domain.Id.sessionIdQuick (Dyn.str input "sessionID")
        do! nudgeHook.handleChatMessage(sessionID, agent, Dyn.get output "parts")
        let message = Dyn.get output "message"
        if not (Dyn.isNullish message) then
            let tools = Dyn.get message "tools"
            if not (Dyn.isNullish tools) then
                setKey message "tools" (resolveChatTools host agent tools)
    }

let chatMessage (registry: ChildAgentRegistry) (nudgeHook: VibeFs.Opencode.NudgeHook.NudgeHook) (input: obj) (output: obj) : JS.Promise<unit> =
    chatMessageFor opencode registry nudgeHook input output

let noop (_a: obj) (_b: obj) : JS.Promise<unit> = Promise.lift ()
let noopEvent (_a: obj) : JS.Promise<unit> = Promise.lift ()
