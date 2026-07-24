namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools

module CompanionTransform =

    let handleTransform (rawOutObj: obj) =
        if not (isNull rawOutObj) && not (isNull rawOutObj?messages) then
            let rawMsgs = unbox<obj list> rawOutObj?messages

            let capsMsg =
                createObj [ "role", box "system"; "text", box "[CAPS: coder, inspector, browser]" ]

            let transformed = Projection.preserveRawTail [ capsMsg ] rawMsgs
            rawOutObj?messages <- List.toArray transformed

    let handleCompanionTransform
        (companions: Dictionary<string, CompanionHost>)
        (gate: obj)
        (sessionPort: ISessionHostPort)
        (inObj: obj)
        (rawOutObj: obj)
        =
        handleTransform rawOutObj

        let sessionId =
            if isNull inObj || isNull inObj?sessionID then
                ""
            else
                unbox<string> inObj?sessionID

        if not (String.IsNullOrWhiteSpace sessionId) && not (isNull rawOutObj?messages) then
            let agentRole =
                if isNull inObj || isNull inObj?agent then
                    None
                else
                    Some(unbox<string> inObj?agent)

            let allowed =
                match agentRole with
                | None -> true
                | Some _ -> Companion.shouldCreateForAgent agentRole

            if allowed then
                let companion =
                    lock gate (fun () ->
                        match companions.TryGetValue sessionId with
                        | true, value -> value
                        | false, _ ->
                            let value = CompanionHost(SessionId.create sessionId, sessionPort)
                            companions.[sessionId] <- value
                            value)

                let rawMsgs = unbox<obj list> rawOutObj?messages
                rawOutObj?messages <- companion.TransformRaw rawMsgs |> List.toArray
