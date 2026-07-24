namespace Wanxiangshu.Next.OpenCode

open System
open Wanxiangshu.Next.Kernel.Identity
open Fable.Core
open Fable.Core.JsInterop

type TerminalOutcome =
    | Completed of messageId: MessageId
    | Aborted of reason: string
    | Failed of error: string

type TerminalCompletionListener = SessionId -> TerminalOutcome -> unit

type IEventObservationPort =
    abstract SubscribeTerminalListener: listener: TerminalCompletionListener -> IDisposable
    abstract NotifyTerminal: sessionId: SessionId -> outcome: TerminalOutcome -> bool
    abstract IsCompleted: sessionId: SessionId -> bool
    abstract GetSessionOutput: sessionId: SessionId -> string list

module Events =

    [<Emit("typeof $0 === 'string' ? $0 : null")>]
    let private jsString (value: obj) : string = jsNative

    type DeterministicEventPort() =
        let listeners = ResizeArray<TerminalCompletionListener>()
        let completedSessions = System.Collections.Generic.HashSet<SessionId>()
        let lockObj = obj ()

        interface IEventObservationPort with
            member _.SubscribeTerminalListener(listener) =
                lock lockObj (fun () -> listeners.Add(listener))

                { new IDisposable with
                    member _.Dispose() =
                        lock lockObj (fun () -> listeners.Remove(listener) |> ignore) }

            member _.NotifyTerminal sessionId outcome =
                let handlers =
                    lock lockObj (fun () ->
                        if completedSessions.Contains(sessionId) then
                            []
                        else
                            completedSessions.Add(sessionId) |> ignore
                            listeners |> Seq.toList)

                if List.isEmpty handlers then
                    false
                else
                    for h in handlers do
                        h sessionId outcome

                    true

            member _.IsCompleted sessionId =
                lock lockObj (fun () -> completedSessions.Contains(sessionId))

            member _.GetSessionOutput _ = []

    type HostEventPort() =
        let listeners = ResizeArray<TerminalCompletionListener>()
        let completedSessions = System.Collections.Generic.HashSet<SessionId>()

        let sessionOutputs =
            System.Collections.Generic.Dictionary<SessionId, ResizeArray<string>>()

        let lockObj = obj ()

        let asString value =
            if isNull value then
                None
            else
                let text = jsString value
                if isNull text then None else Some text

        let firstString values = values |> List.tryPick asString

        let asObjects (value: obj) =
            if isNull value then
                []
            else
                try
                    unbox<obj array> value |> Array.toList
                with _ ->
                    try
                        unbox<obj list> value
                    with _ ->
                        []

        let recordOutput sessionId text =
            lock lockObj (fun () ->
                match sessionOutputs.TryGetValue(sessionId) with
                | true, output -> output.Add(text)
                | false, _ ->
                    let output = ResizeArray<string>()
                    output.Add(text)
                    sessionOutputs.[sessionId] <- output)

        let partText (part: obj) =
            if isNull part then
                None
            else
                let partType = asString part?``type``

                if partType |> Option.exists (fun value -> value <> "text") then
                    None
                else
                    asString part?text
                    |> Option.filter (fun text -> not (String.IsNullOrWhiteSpace text))

        let textFromParts (value: obj) =
            let text = asObjects value |> List.choose partText |> String.concat ""
            if String.IsNullOrWhiteSpace text then None else Some text

        let assistantText (properties: obj) (eventObj: obj) (eventType: string) =
            let message: obj = properties?message

            let role =
                (if not (isNull message) then asString message?role else None)
                |> Option.orElse (asString properties?role)
                |> Option.map (fun value -> value.ToLowerInvariant())

            let isAssistant =
                role = Some "assistant" || (role.IsNone && eventType.Contains("assistant"))

            if not isAssistant then
                None
            else
                match
                    if not (isNull message) then
                        textFromParts message?parts
                    else
                        None
                with
                | Some text -> Some text
                | None ->
                    match textFromParts properties?parts with
                    | Some text -> Some text
                    | None ->
                        asString properties?text
                        |> Option.filter (fun text -> not (String.IsNullOrWhiteSpace text))

        let sessionIdOf properties eventObj =
            [ properties?sessionID
              properties?sessionId
              eventObj?sessionID
              eventObj?sessionId ]
            |> firstString
            |> Option.map SessionId.create

        let messageIdOf properties eventObj =
            let propertyMessage = properties?message
            let propertyInfo = properties?info
            let eventMessage = eventObj?message

            [ properties?messageID
              properties?messageId
              if not (isNull propertyMessage) then
                  propertyMessage?id
              else
                  null
              if not (isNull propertyInfo) then propertyInfo?id else null
              eventObj?messageID
              eventObj?messageId
              if not (isNull eventMessage) then eventMessage?id else null ]
            |> firstString
            |> Option.map MessageId.create

        let errorText properties eventObj =
            let propertyError = properties?error
            let eventError = eventObj?error

            [ if not (isNull propertyError) then
                  propertyError?name
              else
                  null
              if not (isNull propertyError) then
                  propertyError?message
              else
                  null
              propertyError
              if not (isNull eventError) then eventError?name else null
              if not (isNull eventError) then eventError?message else null ]
            |> firstString

        let notify sessionId outcome =
            let handlers =
                lock lockObj (fun () ->
                    if completedSessions.Contains(sessionId) then
                        []
                    else
                        completedSessions.Add(sessionId) |> ignore
                        listeners |> Seq.toList)

            for handler in handlers do
                handler sessionId outcome

        member this.Observe(rawEvent: obj) =
            if not (isNull rawEvent) then
                let eventObj = if isNull rawEvent?event then rawEvent else rawEvent?event

                let properties =
                    if not (isNull eventObj?properties) then eventObj?properties
                    elif not (isNull eventObj?data) then eventObj?data
                    else eventObj

                let eventType =
                    [ eventObj?``type``; eventObj?eventType ]
                    |> firstString
                    |> Option.defaultValue ""
                    |> fun value -> value.ToLowerInvariant()

                let fallbackMessageId =
                    MessageId.create (
                        SessionId.value (
                            sessionIdOf properties eventObj
                            |> Option.defaultValue (SessionId.create "unknown")
                        )
                    )

                match sessionIdOf properties eventObj with
                | None -> ()
                | Some sessionId ->
                    assistantText properties eventObj eventType
                    |> Option.iter (recordOutput sessionId)

                    let outcome =
                        match errorText properties eventObj with
                        | Some error when
                            eventType.Contains("abort")
                            || error.Contains("abort", StringComparison.OrdinalIgnoreCase)
                            ->
                            Some(Aborted error)
                        | Some error when eventType.Contains("error") || eventType.Contains("fail") ->
                            Some(Failed error)
                        | _ when eventType.Contains("abort") -> Some(Aborted "Host reported session abort")
                        | _ when
                            eventType = "session.idle"
                            || (eventType = "session.status" && (asString properties?status = Some "idle"))
                            || eventType.Contains("assistant.completed")
                            || eventType = "message.completed"
                            ->
                            Some(Completed(messageIdOf properties eventObj |> Option.defaultValue fallbackMessageId))
                        | _ when eventType.Contains("message.updated") || eventType.Contains("assistant") ->
                            let message = properties?message
                            let eventTime = properties?time
                            let messageTime = if not (isNull message) then message?time else null

                            let role =
                                [ if not (isNull message) then message?role else null
                                  properties?role ]
                                |> firstString

                            let finished =
                                not (isNull properties?finish)
                                || (not (isNull eventTime) && not (isNull eventTime?completed))
                                || (not (isNull messageTime) && not (isNull messageTime?completed))

                            if role = Some "assistant" && finished then
                                Some(
                                    Completed(messageIdOf properties eventObj |> Option.defaultValue fallbackMessageId)
                                )
                            else
                                None
                        | _ -> None

                    outcome |> Option.iter (notify sessionId)

        interface IEventObservationPort with
            member _.SubscribeTerminalListener(listener) =
                lock lockObj (fun () -> listeners.Add(listener))

                { new IDisposable with
                    member _.Dispose() =
                        lock lockObj (fun () -> listeners.Remove(listener) |> ignore) }

            member _.NotifyTerminal sessionId outcome =
                let before = lock lockObj (fun () -> completedSessions.Contains(sessionId))

                if before then
                    false
                else
                    notify sessionId outcome
                    true

            member _.IsCompleted sessionId =
                lock lockObj (fun () -> completedSessions.Contains(sessionId))

            member _.GetSessionOutput sessionId =
                lock lockObj (fun () ->
                    match sessionOutputs.TryGetValue(sessionId) with
                    | true, output -> output |> Seq.toList
                    | false, _ -> [])

    [<Emit("$0()")>]
    let private invokeDisposer (value: obj) : unit = jsNative

    let trySubscribeHostEvents (input: obj) (port: HostEventPort) : Result<IDisposable option, string> =
        let target =
            if isNull input then
                None
            elif not (isNull input?events) then
                Some input?events
            else
                let client = input?client

                if not (isNull client) && not (isNull client?events) then
                    Some client?events
                else
                    None

        match target with
        | None -> Ok None
        | Some events ->
            let listen = events?listen

            if isNull listen then
                Error "OPENCODE-EVENT-SUBSCRIBE: host event capability exists but events.listen is unavailable"
            else
                try
                    let callback = box (fun rawEvent -> port.Observe rawEvent)
                    let subscription = listen?call (events, callback)

                    if isNull subscription then
                        Error "OPENCODE-EVENT-SUBSCRIBE: events.listen returned no subscription"
                    else
                        Ok(
                            Some(
                                { new IDisposable with
                                    member _.Dispose() = invokeDisposer subscription }
                            )
                        )
                with ex ->
                    Error(sprintf "OPENCODE-EVENT-SUBSCRIBE: %s" ex.Message)
