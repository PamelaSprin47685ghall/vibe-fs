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

    let handleChatMessage (gateway: Gateway) (drivers: SessionDrivers) (input: OpencodeHookInput) (outputObj: obj) =
        let sessionId = SessionId.create input.sessionID

        let key: SessionDriversKey =
            { RuntimeId = gateway.RuntimeId
              SessionId = sessionId }

        if not (isNull outputObj) && not (isNull outputObj?message) then
            let msg = outputObj?message
            let role = unbox<string> msg?role

            if role = "user" then
                let userMsg: OpencodeUserMessage =
                    { id = unbox<string> msg?id
                      role = role
                      sessionID = input.sessionID
                      agent = input.agent
                      model = input.model
                      parts = if isNull msg?parts then [] else unbox<obj list> msg?parts }

                let origin = MessageOriginDecoder.decodeUserMessageOrigin userMsg

                match origin with
                | Human turnId ->
                    drivers.BumpLocalEpochOnHuman key |> ignore
                    let fact = Fact.Session(HumanTurnStarted {| TurnId = turnId |})
                    gateway.Append (StreamId.Session sessionId) (Some turnId) fact |> ignore
                | _ -> ()

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
