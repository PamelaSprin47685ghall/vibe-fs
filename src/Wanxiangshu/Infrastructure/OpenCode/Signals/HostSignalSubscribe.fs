namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
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

    let private subscribeListen (events: obj) (onSignalEvent: obj -> unit) : Result<HostSignalSubscription, string> =
        let listen = events?listen

        if isNull listen then
            Error "OPENCODE-SIGNAL-SUBSCRIBE: events.listen unavailable"
        else
            try
                let callback = box onSignalEvent

                let subscription = listen?call (events, callback)

                if isNull subscription then
                    Error "OPENCODE-SIGNAL-SUBSCRIBE: events.listen returned no subscription"
                else
                    Ok
                        { Health = alwaysHealthy
                          Dispose = fun () -> invokeDisposer subscription }
            with ex ->
                Error(sprintf "OPENCODE-SIGNAL-SUBSCRIBE: %s" ex.Message)

    let trySubscribe
        (input: obj)
        (onSignalEvent: obj -> unit)
        (_timerPort: ITimerPort option)
        : Task<Result<HostSignalSubscription option * string, string>> =
        task {
            let listenTarget =
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

            match listenTarget with
            | Some events ->
                match subscribeListen events onSignalEvent with
                | Ok localSub -> return Ok(Some localSub, "events.listen")
                | Error localError ->
                    Diagnostic.fatal "signal-subscribe-failed" [ "result", localError ]
                    return Error localError

            | None -> return Ok(None, "local-event-hook")
        }
