module Wanxiangshu.Opencode.SessionIo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Messaging
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.ToolContextCodec
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Opopen.MessagingCodec
open Wanxiangshu.Opopen.SubagentSpawn
open Wanxiangshu.Opopen.SubagentIo
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.FallbackRuntimeState

let extractToolContext (context: obj) (pluginDirectory: string) : obj =
    let execution = decodeOpencodeToolContext context pluginDirectory
    box {|
        directory = execution.Directory
        sessionID =
            let s = Id.sessionIdValue execution.SessionId
            if s = "" then box null
            else box s
        abortSignal = SubagentSpawn.getAbortSignal context
    |}

let invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> =
    unbox (target?(method)(arg))

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
                        box {| path = box {| id = sessionId |}; query = box {| directory = directory |} |}
                let! result = invoke1 arg "messages" session
                let data = Dyn.get result "data"
                if Dyn.isNullish data then return []
                else
                    let rawData = unbox<obj[]> data
                    let activeData = rawData |> Array.filter (fun msg ->
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
        with _ -> return []
    }

let extractSessionText = SubagentIo.extractSessionText
let startSubagentSession = SubagentSpawn.startSubagentSession

let promptWithAbort = SubagentIo.promptWithAbort

let runSubagentCoreResult = SubagentSpawn.runSubagentCoreResult

let runSubagentWithCleanup = SubagentSpawn.runSubagentWithCleanup

let runSubagent (runtime: FallbackRuntimeState) (registry: ChildAgentRegistry) (client: obj) (agent: string) (title: string) (prompt: string)
                (directory: string) (sessionID: string) (context: obj)
                (tools: obj) : JS.Promise<Result<string, DomainError>> =
    SubagentSpawn.runSubagentCoreResult runtime registry client agent title prompt directory sessionID context tools false
