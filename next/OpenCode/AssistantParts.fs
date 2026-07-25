namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core.JsInterop

type AssistantParts() =
    let byMessageId = Dictionary<string, Dictionary<string, obj>>()

    let messageIdOf (part: obj) =
        if isNull part then
            None
        elif not (isNull part?messageID) then
            Some(unbox<string> part?messageID)
        elif not (isNull part?messageId) then
            Some(unbox<string> part?messageId)
        else
            None

    member _.Record(properties: obj) =
        let part = if isNull properties then null else properties?part

        match messageIdOf part with
        | Some messageId ->

            let partId =
                if isNull part?id then
                    Guid.NewGuid().ToString("N")
                else
                    unbox<string> part?id

            let parts =
                match byMessageId.TryGetValue messageId with
                | true, existing -> existing
                | false, _ ->
                    let created = Dictionary<string, obj>()
                    byMessageId.[messageId] <- created
                    created

            parts.[partId] <- part
        | None -> ()

    member _.Hydrate(messageId: string, lastMessage: obj) =
        match byMessageId.TryGetValue messageId with
        | true, parts when parts.Count > 0 ->
            createObj [ "info", box lastMessage?info; "parts", box (parts.Values |> Seq.toArray) ]
        | _ -> lastMessage

    member _.Remove(messageId: string) = byMessageId.Remove messageId |> ignore
