namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

module OpencodeHooks =

    let private processUserMessage
        (gateway: Gateway)
        (drivers: SessionDrivers)
        (inboxMap: System.Collections.Generic.Dictionary<SessionId, ISessionInbox>)
        (sessionId: SessionId)
        (userMsg: OpencodeUserMessage)
        =
        let key: SessionDriversKey =
            { RuntimeId = gateway.RuntimeId
              SessionId = sessionId }

        let origin = MessageOriginDecoder.decodeUserMessageOrigin userMsg

        let inbox =
            match inboxMap.TryGetValue(sessionId) with
            | true, ib -> ib
            | false, _ ->
                let ib = FifoInbox(1000) :> ISessionInbox
                inboxMap.[sessionId] <- ib
                ib

        let text =
            userMsg.parts
            |> List.choose (fun p -> if isNull p?text then None else Some(unbox<string> p?text))
            |> String.concat "\n"

        match origin with
        | Human turnId ->
            drivers.BumpLocalEpochOnHuman key |> ignore
            inbox.TryPost(HumanMessageEvent(turnId, text)) |> ignore
            let cts = new CancellationTokenSource()
            drivers.Activate(key, cts) |> ignore
        | PluginGenerated promptKeyRef -> inbox.TryPost(PluginEvent(PromptKeyRef.value promptKeyRef, text)) |> ignore
        | HostInternal -> ()

    let handleChatMessage
        (gateway: Gateway)
        (drivers: SessionDrivers)
        (inboxMap: System.Collections.Generic.Dictionary<SessionId, ISessionInbox>)
        (input: OpencodeHookInput)
        (outputObj: obj)
        =
        if not (isNull outputObj) && not (isNull outputObj?message) then
            let msg = outputObj?message
            let role = unbox<string> msg?role

            if role = "user" then
                let sessionId = SessionId.create input.sessionID

                let rawModel =
                    match input.model with
                    | Some m -> Some m
                    | None ->
                        if not (isNull msg?model) then
                            let mObj = msg?model

                            Some
                                { providerID = unbox<string> mObj?providerID
                                  modelID = unbox<string> mObj?modelID
                                  variant =
                                    if isNull mObj?variant then
                                        None
                                    else
                                        Some(unbox<string> mObj?variant) }
                        else
                            None

                let rawParts = if isNull msg?parts then [] else unbox<obj list> msg?parts

                let rawAgent =
                    match input.agent with
                    | Some a -> Some a
                    | None ->
                        if isNull msg?agent then
                            None
                        else
                            Some(unbox<string> msg?agent)

                let userMsg: OpencodeUserMessage =
                    { id = unbox<string> msg?id
                      role = role
                      sessionID = input.sessionID
                      agent = rawAgent
                      model = rawModel
                      parts = rawParts }

                processUserMessage gateway drivers inboxMap sessionId userMsg

    let handleToolExecuteBefore (input: OpencodeToolExecuteInput) (output: OpencodeToolExecuteOutput) =
        let argsObj = output.args

        if not (isNull argsObj) then
            if not (isNull argsObj?warn_tdd) then
                argsObj?warn_tdd <- null

            if not (isNull argsObj?warn_reuse) then
                argsObj?warn_reuse <- null

            if not (isNull argsObj?warn_context) then
                argsObj?warn_context <- null

    let handleEvent
        (gateway: Gateway)
        (inboxMap: System.Collections.Generic.Dictionary<SessionId, ISessionInbox>)
        (eventObj: obj)
        =
        if not (isNull eventObj) then
            let eventType = unbox<string> eventObj?``type``
            let properties = eventObj?properties

            if not (isNull properties) && not (isNull properties?sessionID) then
                let sessionId = SessionId.create (unbox<string> properties?sessionID)

                match inboxMap.TryGetValue(sessionId) with
                | true, inbox ->
                    match eventType with
                    | "session.idle" -> inbox.TryPost(LifecycleEvent "session.idle") |> ignore
                    | "session.status" ->
                        let statusType = unbox<string> properties?status?``type``
                        inbox.TryPost(LifecycleEvent($"session.status:{statusType}")) |> ignore
                    | "message.updated" ->
                        let msgObj = properties?info

                        if not (isNull msgObj) then
                            let role = unbox<string> msgObj?role

                            if role = "assistant" then
                                let msgId = MessageId.create (unbox<string> msgObj?id)

                                let parentId =
                                    if isNull msgObj?parentID then
                                        MessageId.create ""
                                    else
                                        MessageId.create (unbox<string> msgObj?parentID)

                                let isError = not (isNull msgObj?error)

                                let outcome =
                                    if isError then
                                        PromptOutcome.FatalFailure "assistant-error"
                                    else
                                        PromptOutcome.Delivered msgId

                                inbox.TryPost(AssistantTerminalEvent(parentId, msgId, outcome)) |> ignore
                    | _ -> ()
                | false, _ -> ()
