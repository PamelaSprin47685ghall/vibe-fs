module Wanxiangshu.Opencode.SubagentIo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Opencode.MessagingCodec

let noOutputText = "(no output)"

let extractSessionText (client: obj) (sessionId: string) (directory: string) : JS.Promise<string> =
    promise {
        try
            match getSessionApiFromClient client with
            | Error _ -> return noOutputText
            | Ok session ->
                let arg =
                    if directory = "" then
                        box {| path = box {| id = sessionId |} |}
                    else
                        box {| path = box {| id = sessionId |}; query = box {| directory = directory |} |}
                let! result = (session?messages)(arg)
                let data = Dyn.get result "data"
                if Dyn.isNullish data then return noOutputText
                else
                    let messagesList = MessagingCodec.decodeMessages (unbox<obj[]> data)
                    match Messaging.readAssistantText messagesList 0 "\n\n" with
                    | Some text -> return text
                    | None -> return noOutputText
        with _ -> return noOutputText
    }

let buildPromptBody (options: {| agent: string; prompt: string; directory: string; sessionID: string; tools: obj; aiSettings: {| modelString: string option; thinkingLevel: string option |} |}) childID : obj =
    let body = box {| agent = options.agent; parts = [| box {| ``type`` = "text"; text = options.prompt |} |] |}
    let body = if Dyn.isNullish options.tools then body else Dyn.withKey body "tools" options.tools
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

let promptWithAbort (client: obj) (args: obj) (signal: obj) : JS.Promise<unit> =
    promise {
        match getSessionApiFromClient client with
        | Error err -> return! Promise.reject (exn (wireEncodeToolError "OpencodeClient" err))
        | Ok session ->
            if Dyn.isNullish signal then
                do! session?prompt(args)
            elif Dyn.truthy (Dyn.get signal "aborted") then
                return! Promise.reject (DOMException("Aborted", "AbortError"))
            else
                let settled = ref false
                let handlerRef = ref None
                let abortAsync : JS.Promise<string> =
                    Promise.create (fun resolve _reject ->
                        let handler = fun () ->
                            if not settled.Value then
                                settled.Value <- true
                                match handlerRef.Value with
                                | Some h -> signal?removeEventListener("abort", h) |> ignore
                                | None -> ()
                                resolve "aborted"
                        handlerRef.Value <- Some handler
                        signal?addEventListener("abort", handler) |> ignore)
                let promptAsync : JS.Promise<string> =
                    promise {
                        do! session?prompt(args)
                        if not settled.Value then
                            settled.Value <- true
                            match handlerRef.Value with
                            | Some h -> signal?removeEventListener("abort", h) |> ignore
                            | None -> ()
                        return "ok"
                    }
                try
                    let! winner = Promise.race [ promptAsync; abortAsync ]
                    if winner = "aborted" then return! Promise.reject (DOMException("Aborted", "AbortError"))
                with err ->
                    match translateJsError err with
                    | MessageAborted -> return! Promise.reject (DOMException("Aborted", "AbortError"))
                    | _ -> return! Promise.reject err
    }
