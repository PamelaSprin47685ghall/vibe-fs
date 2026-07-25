namespace Wanxiangshu.Next.OpenCode

open System.Collections.Generic
open Fable.Core.JsInterop

type AbortTracker() =
    let aborted = HashSet<string>()

    let isUserMessage (raw: obj) =
        let event = if isNull raw || isNull raw?event then raw else raw?event
        let properties = if isNull event then null else event?properties
        let message = if isNull properties then null else properties?message
        let target = if isNull message then properties else message
        let info = if isNull target then null else target?info

        let role =
            if not (isNull info) && not (isNull info?role) then
                unbox<string> info?role
            elif not (isNull target) && not (isNull target?role) then
                unbox<string> target?role
            else
                ""

        not (isNull event) && event?``type`` = "message.updated" && role = "user"

    member _.Mark(sessionId: string) = aborted.Add sessionId |> ignore
    member _.Contains(sessionId: string) = aborted.Contains sessionId

    member _.Observe(raw: obj, sessionId: string) =
        if isUserMessage raw then
            aborted.Remove sessionId |> ignore
