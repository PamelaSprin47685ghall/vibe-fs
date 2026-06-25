module VibeFs.Opencode.KnowledgeGraphSessionMessages

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.Messaging
open VibeFs.Opencode.MessagingCodec
open VibeFs.Shell.Dyn
open VibeFs.Shell.OpencodeClientCodec

let private invoke1 (target: obj) (methodName: string) (arg: obj) : JS.Promise<obj> =
    unbox (target?(methodName)(arg))

let fetchSessionMessageArray (client: obj) (directory: string) (sessionID: string) : JS.Promise<obj array option> =
    promise {
        if sessionID.Trim() = "" || isNullish client then return None
        else
            try
                match getSessionApiFromClient client with
                | Error _ -> return None
                | Ok session ->
                    let! result =
                        invoke1 session "messages" (box {| path = box {| id = sessionID |}; query = box {| directory = directory |} |})
                    let data = get result "data"
                    if isNullish data || not (isArray data) then return None
                    else return Some (unbox<obj array> data)
            with _ ->
                return None
    }

let loadSessionMessages (client: obj) (directory: string) (sessionID: string) : JS.Promise<Message<obj> list> =
    promise {
        let! raw = fetchSessionMessageArray client directory sessionID
        match raw with
        | None -> return []
        | Some data -> return MessagingCodec.decodeMessages data
    }

let tryResolveJobContext (client: obj) (directory: string) (sessionID: string) : JS.Promise<KnowledgeGraphJobContext option> =
    promise {
        let! raw = fetchSessionMessageArray client directory sessionID
        match raw with
        | None -> return None
        | Some data ->
            let messages = MessagingCodec.decodeMessages data
            let texts =
                messages
                |> flatten
                |> List.choose (fun fp ->
                    match fp.part with
                    | TextPart text -> Some text
                    | _ -> None)
            return texts |> List.tryPick tryParseJobMarker
    }