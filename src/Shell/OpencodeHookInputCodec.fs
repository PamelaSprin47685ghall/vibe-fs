module VibeFs.Shell.OpencodeHookInputCodec

open VibeFs.Kernel.Messaging
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.Dyn
open VibeFs.Shell.ToolContextCodec

type HostEventEnvelope = { EventType: string; Props: obj }

let sessionIdFromHookInput (input: obj) (fallbackDir: string) : string =
    (decodeOpencodeToolContext input fallbackDir).SessionId

let toolNameFromHookInput (input: obj) : string = Dyn.str input "tool"

let argsFromHookInput (input: obj) : obj = Dyn.get input "args"

let toolIdFromDefinitionHookInput (input: obj) : string = Dyn.str input "toolID"

let executorModeFromHookInput (input: obj) : string =
    Dyn.str (argsFromHookInput input) "mode"

let selectMethodologiesFromHookArgs (args: obj) : string list =
    let raw = if Dyn.isNullish args then null else Dyn.get args "select_methodology"
    if Dyn.isNullish raw || not (Dyn.isArray raw) then []
    else raw :?> obj array |> Array.map string |> Array.toList

let decodeHostEventEnvelope (input: obj) : HostEventEnvelope option =
    let event = Dyn.get input "event"
    if Dyn.isNullish event then None
    else
        let eventType = Dyn.str event "type"
        let rawProps = Dyn.get event "properties"
        let props = if Dyn.isNullish rawProps then event else rawProps
        Some { EventType = eventType; Props = props }

let hookOutputError (output: obj) : string = Dyn.str output "error"

let hookOutputText (output: obj) : string = Dyn.str output "output"

let private resolveAgentFromMessage (registry: ChildAgentRegistry) (message: obj) : string option =
    if Dyn.isNullish message then None
    else
        let info = Dyn.get message "info"
        if Dyn.isNullish info then None
        else
            let agent = Dyn.str info "agent"
            if agent <> "" then Some agent
            else
                let sessionID = Dyn.str info "sessionID"
                if sessionID = "" then None else registry.LookupChildAgent sessionID

let explicitAgentFromHookInput (input: obj) : string = Dyn.str input "agent"

let commandNameFromHookInput (input: obj) : string = Dyn.str input "command"

let commandArgumentsFromHookInput (input: obj) : string = Dyn.str input "arguments"

let agentFromMessageInfo (registry: ChildAgentRegistry) (info: MessageInfo<obj>) : string option =
    if info.agent <> "" then Some info.agent
    elif info.sessionID <> "" then registry.LookupChildAgent info.sessionID
    else None

let resolveAgentFromMessages (registry: ChildAgentRegistry) (messages: Message<obj> list) : string option =
    let fromInfo = agentFromMessageInfo registry
    let tryAgentBack (predicate: Message<obj> -> bool) : string option =
        messages
        |> List.filter predicate
        |> List.tryLast
        |> Option.bind (fun m -> fromInfo m.info)
    [ tryAgentBack (fun m -> m.info.role = User && m.source = Native)
      tryAgentBack (fun m -> m.info.role = Assistant)
      tryAgentBack (fun m -> fromInfo m.info |> Option.isSome) ]
    |> List.tryPick id

let resolveMessagesTransformAgent (registry: ChildAgentRegistry) (input: obj) (messages: Message<obj> list) (defaultAgent: string) : string =
    let explicit = explicitAgentFromHookInput input
    if explicit <> "" then explicit
    else
        match registry.LookupChildAgent (sessionIdFromHookInput input "") with
        | Some a -> a
        | None -> resolveAgentFromMessages registry messages |> Option.defaultValue defaultAgent

let resolveHookAgent (registry: ChildAgentRegistry) (input: obj) (outputOpt: obj option) (defaultAgent: string) : string =
    let explicit = explicitAgentFromHookInput input
    if explicit <> "" then explicit
    else
        match registry.LookupChildAgent (sessionIdFromHookInput input "") with
        | Some a -> a
        | None ->
            match outputOpt with
            | Some output -> resolveAgentFromMessage registry (Dyn.get output "message")
            | None -> None
            |> Option.defaultValue defaultAgent