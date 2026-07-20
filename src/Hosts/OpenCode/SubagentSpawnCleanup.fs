module Wanxiangshu.Hosts.Opencode.SubagentSpawnCleanup

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Hosts.Opencode.MessagingCodec

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Hosts.Opencode.SubagentSpawnInput

let noOutputText = "(no output)"
let abortedPrefix = "(aborted)"

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

                    match readAssistantText messagesList finalStartIndex "\n\n" with
                    | Some text -> return text
                    | None -> return noOutputText
        with _ ->
            return noOutputText
    }

let physicalAbort (session: obj) (childID: string) : JS.Promise<unit> =
    promise {
        if Dyn.isNullish (Dyn.get session "abort") then
            ()
        else
            try
                let arg = box {| path = box {| id = childID |} |}
                let! _ = invoke1 arg "abort" session
                ()
            with _ ->
                ()
    }
