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
            let rawMsgs = unbox<obj array> rawOutObj?messages |> Array.toList

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

        let rawMsgs = unbox<obj array> rawOutObj?messages |> Array.toList

        let messageContext =
            rawMsgs
            |> List.tryPick (fun message ->
                if isNull message || isNull message?info then
                    None
                else
                    let sessionId =
                        if isNull message?info?sessionID then
                            None
                        else
                            Some(unbox<string> message?info?sessionID)

                    let role =
                        if isNull message?info?agent then
                            None
                        else
                            Some(unbox<string> message?info?agent)

                    Some(sessionId, role))

        match messageContext with
        | Some(Some sessionId, _) when not (isNull inObj) && isNull inObj?sessionID -> inObj?sessionID <- sessionId
        | _ -> ()

        let sessionId =
            if isNull inObj || isNull inObj?sessionID then
                ""
            else
                unbox<string> inObj?sessionID

        if not (String.IsNullOrWhiteSpace sessionId) && not (isNull rawOutObj?messages) then
            let agentRole =
                match messageContext |> Option.bind snd with
                | Some role -> Some role
                | None when not (isNull inObj) && not (isNull inObj?agent) -> Some(unbox<string> inObj?agent)
                | None -> None

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

                let rawMsgs = unbox<obj array> rawOutObj?messages |> Array.toList
                rawOutObj?messages <- companion.TransformRaw rawMsgs |> List.toArray
