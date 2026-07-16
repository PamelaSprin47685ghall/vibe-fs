module Wanxiangshu.Runtime.OpencodeHookInputCodec

open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.ReviewPrompts
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.ToolContextCodec

type HostEventEnvelope = { EventType: string; Props: obj }

let sessionIdFromHookInput (input: obj) (fallbackDir: string) : string =
    Id.sessionIdValue
        (decodeOpencodeToolContext (unbox<IOpenCodeToolContext> input) fallbackDir)
            .SessionId

let toolNameFromHookInput (input: obj) : string = Dyn.str input "tool"

let argsFromHookInput (input: obj) : obj = Dyn.get input "args"

let toolIdFromDefinitionHookInput (input: obj) : string = Dyn.str input "toolID"

let executorModeFromHookInput (input: obj) : string =
    Dyn.str (argsFromHookInput input) "mode"

let selectMethodologiesFromHookArgs (args: obj) : string list =
    let raw =
        if Dyn.isNullish args then
            null
        else
            Dyn.get args "select_methodology"

    if Dyn.isNullish raw || not (Dyn.isArray raw) then
        []
    else
        raw :?> obj array |> Array.map string |> Array.toList

let decodeHostEventEnvelope (input: obj) : HostEventEnvelope option =
    let event = Dyn.get input "event"

    if Dyn.isNullish event then
        None
    else
        let eventType = Dyn.str event "type"
        let rawProps = Dyn.get event "properties"
        let props = if Dyn.isNullish rawProps then event else rawProps
        Some { EventType = eventType; Props = props }

let hookOutputError (output: obj) : string = Dyn.str output "error"

let hookOutputText (output: obj) : string = Dyn.str output "output"

let hookOutputString (output: obj) : string option =
    let out = Dyn.get output "output"

    if Dyn.isNullish out || not (Dyn.typeIs out "string") then
        None
    else
        Some(unbox<string> out)

let setHookOutputString (output: obj) (text: string) : unit = output?("output") <- text

let partsFromHookOutput (output: obj) : obj = Dyn.get output "parts"

/// Read output.args (tool execute args rewriter payload); absent yields `obj` sentinel.
let argsFromHookOutput (output: obj) : obj = Dyn.get output "args"

/// Write output.args — host wire SSOT for tool execute args rewriter.
let setHookArgs (output: obj) (args: obj) : unit = output?("args") <- args

/// Args for `tool.execute.before`: prefer `output.args` (host rewriter slot), else
/// `input.args`, else empty object written onto `output` so warn/ui_ hooks always run.
let resolveHookExecuteArgs (input: obj) (output: obj) : obj =
    let fromOutput = argsFromHookOutput output

    if not (Dyn.isNullish fromOutput) then
        fromOutput
    else
        let fromInput = argsFromHookInput input
        let args = if Dyn.isNullish fromInput then createObj [] else fromInput
        setHookArgs output args
        args

/// Write output.error — host wire SSOT for tool execute error payload.
let setHookError (output: obj) (error: string) : unit = output?("error") <- box error

/// Write output.parts — host wire SSOT for command/chat reply parts.
let setHookParts (output: obj) (parts: obj) : unit = output?("parts") <- parts

/// Read output.error optional string (absent → None, present non-string → None).
let hookOutputErrorOpt (output: obj) : string option =
    let raw = Dyn.get output "error"

    if Dyn.isNullish raw || not (Dyn.typeIs raw "string") then
        None
    else
        Some(unbox<string> raw)

let private resolveAgentFromMessage (registry: ChildAgentRegistry) (message: obj) : string option =
    if Dyn.isNullish message then
        None
    else
        let info = Dyn.get message "info"

        if Dyn.isNullish info then
            None
        else
            let agent = Dyn.str info "agent"

            if agent <> "" then
                Some agent
            else
                let sessionID = Dyn.str info "sessionID"

                if sessionID = "" then
                    None
                else
                    registry.LookupChildAgent sessionID

let explicitAgentFromHookInput (input: obj) : string = Dyn.str input "agent"

let commandNameFromHookInput (input: obj) : string = Dyn.str input "command"

let commandArgumentsFromHookInput (input: obj) : string = Dyn.str input "arguments"

let agentFromMessageInfo (registry: ChildAgentRegistry) (info: MessageInfo<obj>) : string option =
    if info.agent <> "" then
        Some info.agent
    elif info.sessionID <> "" then
        registry.LookupChildAgent info.sessionID
    else
        None

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

let resolveMessagesTransformAgent
    (registry: ChildAgentRegistry)
    (input: obj)
    (messages: Message<obj> list)
    (defaultAgent: string)
    : string =
    let explicit = explicitAgentFromHookInput input

    if explicit <> "" then
        explicit
    else
        match registry.LookupChildAgent(sessionIdFromHookInput input "") with
        | Some a -> a
        | None -> resolveAgentFromMessages registry messages |> Option.defaultValue defaultAgent

let resolveHookAgent
    (registry: ChildAgentRegistry)
    (input: obj)
    (outputOpt: obj option)
    (defaultAgent: string)
    : string =
    let explicit = explicitAgentFromHookInput input

    if explicit <> "" then
        explicit
    else
        match registry.LookupChildAgent(sessionIdFromHookInput input "") with
        | Some a -> a
        | None ->
            match outputOpt with
            | Some output -> resolveAgentFromMessage registry (Dyn.get output "message")
            | None -> None
            |> Option.defaultValue defaultAgent

let private setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v

/// Ensure a slash-command template entry exists in the cfg.command object.
/// Creates cfg.command as emptyObj if absent; fills `name` only when missing.
let ensureCommandTemplate (cfg: obj) (name: string) (template: string) (description: string) : unit =
    let cmd = Dyn.get cfg "command"
    let cmdObj = if Dyn.isNullish cmd then createObj [] else cmd

    if Dyn.isNullish (Dyn.get cmdObj name) then
        setKey
            cmdObj
            name
            (box
                {| template = template
                   description = description |})

    setKey cfg "command" cmdObj

/// Register /loop and /loop-review command templates from ReviewPrompts constants.
let registerLoopReviewCommands (cfg: obj) : unit =
    ensureCommandTemplate
        cfg
        "loop"
        withReviewCommandTemplate
        "Enable With-Review Mode — the next submission must pass through a reviewer before being accepted"

    ensureCommandTemplate
        cfg
        "loop-review"
        withReviewPrecheckCommandTemplate
        "Enable With-Review Mode with pre-review — the task is pre-reviewed immediately, and reviewer feedback is prepended to your prompt before any work begins"
