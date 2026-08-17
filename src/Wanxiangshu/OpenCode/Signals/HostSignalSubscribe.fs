namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Process

/// Subscribes coarse host signals for the plugin instance.
/// The standard OpenCode contract delivers directory-scoped events via the
/// in-process `hooks.event` hook (`local-event-hook`). Legacy `events.listen`
/// is supported when passed explicitly on input.
module HostSignalSubscribe =

    /// Snapshot of the signal transport.
    type SignalHealth =
        { IsConnected: bool
          ReconnectAttempts: int }

    /// Disposable subscription plus a health probe for downstream consumers.
    type HostSignalSubscription =
        { Health: unit -> SignalHealth
          Dispose: unit -> unit }

    [<Emit("$0()")>]
    let private invokeDisposer (value: obj) : unit = jsNative

    let private alwaysHealthy () : SignalHealth =
        { IsConnected = true
          ReconnectAttempts = 0 }

    let private subscribeValue events listen onSignalEvent =
        let callback = box onSignalEvent
        let subscription = listen?call (events, callback)

        if isNull subscription then
            Error "OPENCODE-SIGNAL-SUBSCRIBE: events.listen returned no subscription"
        else
            Ok
                { Health = alwaysHealthy
                  Dispose = fun () -> invokeDisposer subscription }

    let private trySubscribeValue events listen onSignalEvent =
        try
            subscribeValue events listen onSignalEvent
        with ex ->
            Error(sprintf "OPENCODE-SIGNAL-SUBSCRIBE: %s" ex.Message)

    let private subscribeListen (events: obj) (onSignalEvent: obj -> unit) : Result<HostSignalSubscription, string> =
        let listen = events?listen

        if isNull listen then
            Error "OPENCODE-SIGNAL-SUBSCRIBE: events.listen unavailable"
        else
            trySubscribeValue events listen onSignalEvent

    let private clientEvents client =
        if not (isNull client) && not (isNull client?events) then Some client?events else None

    let private listenTargetFromInput input =
        if not (isNull input?events) then Some input?events else clientEvents input?client

    let private listenTarget input =
        if isNull input then None else listenTargetFromInput input

    let private subscribeTarget events onSignalEvent =
        match subscribeListen events onSignalEvent with
        | Ok localSub -> Ok(Some localSub, "events.listen")
        | Error localError ->
            Diagnostic.fatal "signal-subscribe-failed" [ "result", localError ]
            Error localError

    let trySubscribe
        (input: obj)
        (onSignalEvent: obj -> unit)
        (_timerPort: ITimerPort option)
        : Task<Result<HostSignalSubscription option * string, string>> =
        task {
            match listenTarget input with
            | Some events -> return subscribeTarget events onSignalEvent
            | None -> return Ok(None, "local-event-hook")
        }
