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

/// Optional output boundary used to isolate one prompt/run from prior session history.
/// Kept separate from IEventObservationPort so existing event-port implementations remain compatible.
type IEventOutputBoundaryPort =
    abstract GetSessionOutputWatermark: sessionId: SessionId -> int
    abstract GetSessionOutputSince: sessionId: SessionId * watermark: int -> string list

module Events =

    type DeterministicEventPort() =
        let listeners = ResizeArray<TerminalCompletionListener>()
        let lockObj = obj ()

        interface IEventObservationPort with
            member _.SubscribeTerminalListener(listener) =
                lock lockObj (fun () -> listeners.Add(listener))

                { new IDisposable with
                    member _.Dispose() =
                        lock lockObj (fun () -> listeners.Remove(listener) |> ignore) }

            member _.NotifyTerminal sessionId outcome =
                let handlers = lock lockObj (fun () -> listeners |> Seq.toList)

                if List.isEmpty handlers then
                    false
                else
                    for h in handlers do
                        h sessionId outcome

                    true

            member _.IsCompleted sessionId = false

            member _.GetSessionOutput _ = []

        interface IEventOutputBoundaryPort with
            member _.GetSessionOutputWatermark _ = 0
            member _.GetSessionOutputSince(_, _) = []

    type HostEventPort() =
        let listeners = ResizeArray<TerminalCompletionListener>()

        let sessionOutputs =
            System.Collections.Generic.Dictionary<SessionId, ResizeArray<string>>()

        let lockObj = obj ()

        let recordOutput sessionId text =
            lock lockObj (fun () ->
                match sessionOutputs.TryGetValue(sessionId) with
                | true, output -> output.Add(text)
                | false, _ ->
                    let output = ResizeArray<string>()
                    output.Add(text)
                    sessionOutputs.[sessionId] <- output)

        let notify sessionId outcome =
            let handlers = lock lockObj (fun () -> listeners |> Seq.toList)

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
                    |> MessageOriginDecoder.firstString
                    |> Option.defaultValue ""
                    |> fun value -> value.ToLowerInvariant()

                let fallbackMessageId =
                    MessageId.create (
                        SessionId.value (
                            MessageOriginDecoder.sessionIdOf properties eventObj
                            |> Option.defaultValue (SessionId.create "unknown")
                        )
                    )

                match MessageOriginDecoder.sessionIdOf properties eventObj with
                | None -> ()
                | Some sessionId ->
                    MessageOriginDecoder.assistantText properties eventObj eventType
                    |> Option.iter (recordOutput sessionId)

                    let outcome =
                        match MessageOriginDecoder.errorText properties eventObj with
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
                            || (eventType = "session.status"
                                && (MessageOriginDecoder.asString properties?status = Some "idle"))
                            || eventType.Contains("assistant.completed")
                            || eventType = "message.completed"
                            ->
                            Some(
                                Completed(
                                    MessageOriginDecoder.messageIdOf properties eventObj
                                    |> Option.defaultValue fallbackMessageId
                                )
                            )
                        | _ when eventType.Contains("message.updated") || eventType.Contains("assistant") ->
                            let message = properties?message
                            let eventTime = properties?time
                            let messageTime = if not (isNull message) then message?time else null

                            let role =
                                [ if not (isNull message) then message?role else null
                                  properties?role ]
                                |> MessageOriginDecoder.firstString

                            let finished =
                                not (isNull properties?finish)
                                || (not (isNull eventTime) && not (isNull eventTime?completed))
                                || (not (isNull messageTime) && not (isNull messageTime?completed))

                            if role = Some "assistant" && finished then
                                Some(
                                    Completed(
                                        MessageOriginDecoder.messageIdOf properties eventObj
                                        |> Option.defaultValue fallbackMessageId
                                    )
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
                let hasListeners = lock lockObj (fun () -> listeners.Count > 0)
                notify sessionId outcome
                hasListeners

            member _.IsCompleted sessionId = false

            member _.GetSessionOutput sessionId =
                lock lockObj (fun () ->
                    match sessionOutputs.TryGetValue(sessionId) with
                    | true, output -> output |> Seq.toList
                    | false, _ -> [])

        interface IEventOutputBoundaryPort with
            member _.GetSessionOutputWatermark sessionId =
                lock lockObj (fun () ->
                    match sessionOutputs.TryGetValue(sessionId) with
                    | true, output -> output.Count
                    | false, _ -> 0)

            member _.GetSessionOutputSince(sessionId, watermark) =
                lock lockObj (fun () ->
                    match sessionOutputs.TryGetValue(sessionId) with
                    | true, output ->
                        let start = max 0 (min watermark output.Count)
                        output |> Seq.skip start |> Seq.toList
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
