module Wanxiangshu.Hosts.Opencode.SessionIo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.Messaging

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Runtime.ToolContextCodec
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Hosts.Opencode.MessagingCodec
open Wanxiangshu.Hosts.Opencode.SubagentTypes
open Wanxiangshu.Hosts.Opencode.SubagentSpawnInput
open Wanxiangshu.Hosts.Opencode.SubagentSpawnCleanup
open Wanxiangshu.Hosts.Opencode.SubagentSpawnTransport
open Wanxiangshu.Hosts.Opencode.SubagentIoRun
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.Fallback.RuntimeStore

let extractToolContext (context: obj) (pluginDirectory: string) : obj =
    let execution =
        decodeOpencodeToolContext (unbox<IOpenCodeToolContext> context) pluginDirectory

    box
        {| directory = execution.Directory
           sessionID =
            let s = Id.sessionIdValue execution.SessionId
            if s = "" then box null else box s
           abortSignal = getAbortSignal context |}

let invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> = unbox (target?(method) (arg))

let readSessionTexts (client: obj) (sessionId: string) (directory: string) : JS.Promise<string list> =
    promise {
        try
            match getSessionApiFromClient client with
            | Error _ -> return []
            | Ok session ->
                let arg =
                    if directory = "" then
                        box {| path = box {| id = sessionId |} |}
                    else
                        box
                            {| path = box {| id = sessionId |}
                               query = box {| directory = directory |} |}

                let! result = invoke1 arg "messages" session
                let data = Dyn.get result "data"

                if Dyn.isNullish data then
                    return []
                else
                    let rawData = unbox<obj[]> data

                    let activeData =
                        rawData
                        |> Array.filter (fun msg ->
                            let info = Dyn.get msg "info"
                            not (Dyn.truthy (Dyn.get info "reverted")))

                    let messagesList = MessagingCodec.decodeMessages activeData

                    return
                        messagesList
                        |> Messaging.flatten
                        |> List.map (fun fp ->
                            match fp.part with
                            | TextPart text -> text
                            | ToolPart(_, _, Some state, _) -> state.output
                            | _ -> "")
        with _ ->
            return []
    }

let extractSessionText = extractSessionText
let startSubagentSession = startSubagentSession

let promptWithAbort = promptWithAbort

let runSubagent
    (runtime: FallbackRuntimeStore)
    (registry: ChildAgentRegistry)
    (client: obj)
    (agent: string)
    (title: string)
    (prompt: string)
    (directory: string)
    (sessionID: string)
    (context: obj)
    (tools: obj)
    : JS.Promise<Result<string, DomainError>> =
    runSubagentCoreResult runtime registry client agent title prompt directory sessionID context tools false None
