module Wanxiangshu.Hosts.Opencode.SubagentSpawn

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Hosts.Opencode.MessagingCodec
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.DelegatedAiSettings
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.GateTransitions

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.OpencodeContextCodec
open Wanxiangshu.Runtime.OpencodeSessionPromptCodec
open Wanxiangshu.Runtime.OpencodeSessionSpawnCodec
open Wanxiangshu.Runtime.SessionIoSpawn
open Wanxiangshu.Hosts.Opencode.SubagentTypes

let noOutputText = "(no output)"
let abortedPrefix = "(aborted)"

let getAbortSignal (context: obj) : obj = getAbortSignalFromContext context

let invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> = unbox (target?(method) (arg))

let buildPromptBody (options: SubagentLaunchOptions) childID : obj =
    let body =
        box
            {| agent = options.agent
               parts =
                [| box
                       {| ``type`` = "text"
                          text = options.prompt |} |] |}

    let body =
        if Dyn.isNullish options.tools then
            body
        else
            Dyn.withKey body "tools" options.tools

    let body =
        match options.aiSettings.modelString with
        | None -> body
        | Some modelString ->
            let payload = createObj [ "modelString", box modelString ]

            match tryDecodePromptModelFromPayload payload with
            | Some model -> Dyn.withKey body "model" model
            | None -> body

    let body =
        match options.aiSettings.thinkingLevel with
        | Some level when level.Trim() <> "" -> Dyn.withKey body "variant" (box level)
        | _ -> body

    createObj [ "path", box {| id = childID |}; "body", body ]

let extractSessionText (client: obj) (sessionId: string) (directory: string) (startIndex: int) : JS.Promise<string> =
    promise {
        try
            match getSessionApiFromClient client with
            | Error _ -> return noOutputText
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
                    return noOutputText
                else
                    let messagesList = MessagingCodec.decodeMessages (unbox<obj[]> data)

                    let lastUserIdx =
                        messagesList
                        |> List.tryFindIndexBack (fun m -> m.info.role = User)
                        |> Option.defaultValue 0

                    let finalStartIndex =
                        if startIndex >= List.length messagesList then lastUserIdx
                        else if lastUserIdx >= startIndex then lastUserIdx
                        else startIndex

                    match Messaging.readAssistantText messagesList finalStartIndex "\n\n" with
                    | Some text -> return text
                    | None -> return noOutputText
        with _ ->
            return noOutputText
    }

let promptWithAbort (client: obj) (args: obj) (signal: obj) : JS.Promise<unit> =
    promise {
        match getSessionApiFromClient client with
        | Error err -> return! Promise.reject (exn (wireEncodeToolError "OpencodeClient" err))
        | Ok session ->
            if Dyn.isNullish signal then
                do! session?prompt (args)
            elif Dyn.truthy (Dyn.get signal "aborted") then
                return! Promise.reject (DOMException("Aborted", "AbortError"))
            else
                let settled = ref false
                let handlerRef = ref None

                let abortAsync: JS.Promise<string> =
                    Promise.create (fun resolve _reject ->
                        let handler =
                            fun () ->
                                if not settled.Value then
                                    settled.Value <- true

                                    match handlerRef.Value with
                                    | Some h -> signal?removeEventListener ("abort", h) |> ignore
                                    | None -> ()

                                    resolve "aborted"

                        handlerRef.Value <- Some handler
                        signal?addEventListener ("abort", handler) |> ignore)

                let promptAsync: JS.Promise<string> =
                    promise {
                        do! session?prompt (args)

                        if not settled.Value then
                            settled.Value <- true

                            match handlerRef.Value with
                            | Some h -> signal?removeEventListener ("abort", h) |> ignore
                            | None -> ()

                        return "ok"
                    }

                try
                    let! winner = Promise.race [ promptAsync; abortAsync ]

                    if winner = "aborted" then
                        return! Promise.reject (DOMException("Aborted", "AbortError"))
                with err ->
                    match translateJsError err with
                    | MessageAborted
                    | ClientCancellation _ -> return! Promise.reject (DOMException("Aborted", "AbortError"))
                    | _ -> return! Promise.reject err
    }

let startSubagentSession
    (registry: ChildAgentRegistry)
    (client: obj)
    (options: SubagentLaunchOptions)
    : JS.Promise<Result<string, DomainError>> =
    promise {
        match getSessionApiFromClient client with
        | Error err -> return Error err
        | Ok session ->
            let parentID =
                registry.ResolveSubsessionParentID(
                    if options.sessionID = "" then
                        None
                    else
                        Some options.sessionID
                )

            let createBody =
                box
                    {| query = box {| directory = options.directory |}
                       body =
                        box
                            {| parentID =
                                (match parentID with
                                 | Some p -> box p
                                 | None -> box null)
                               title = options.title |} |}

            let! createResult = invoke1 createBody "create" session

            match decodeChildSessionIdFromCreateResult createResult with
            | Error err -> return Error err
            | Ok childID ->
                registry.RegisterChildAgent(childID, options.agent, parentID)
                return Ok childID
    }
