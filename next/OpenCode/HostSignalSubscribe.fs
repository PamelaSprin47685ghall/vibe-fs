namespace Wanxiangshu.Next.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop

/// Subscribes coarse host signals from exactly one source per plugin instance:
/// local events.listen is preferred; if unavailable, global SSE is used.
/// Both unavailable is a hard failure — no dual delivery, no silent degradation.
module HostSignalSubscribe =

    [<Emit("$0()")>]
    let private invokeDisposer (value: obj) : unit = jsNative

    [<Emit("new AbortController()")>]
    let private newAbortController () : obj = jsNative

    [<Emit("console.info($0, $1)")>]
    let private logInfo (prefix: string) (message: string) : unit = jsNative

    let private eventTypeOf (raw: obj) =
        if isNull raw then
            ""
        elif not (isNull raw?``type``) then
            unbox<string> raw?``type``
        elif not (isNull raw?payload) && not (isNull raw?payload?``type``) then
            unbox<string> raw?payload?``type``
        else
            ""

    let private isSignalEvent (raw: obj) =
        match eventTypeOf raw with
        | "session.status"
        | "session.error"
        | "session.deleted" -> true
        | _ -> false

    let private unwrap (raw: obj) =
        if isNull raw then
            raw
        elif not (isNull raw?payload) then
            let payload = raw?payload

            if not (isNull raw?directory) && isNull payload?directory then
                payload?directory <- raw?directory

            payload
        else
            raw

    let private subscribeListen (events: obj) (onSignalEvent: obj -> unit) : Result<IDisposable, string> =
        let listen = events?listen

        if isNull listen then
            Error "OPENCODE-SIGNAL-SUBSCRIBE: events.listen unavailable"
        else
            try
                let callback =
                    box (fun rawEvent ->
                        if isSignalEvent rawEvent then
                            onSignalEvent (unwrap rawEvent))

                let subscription = listen?call (events, callback)

                if isNull subscription then
                    Error "OPENCODE-SIGNAL-SUBSCRIBE: events.listen returned no subscription"
                else
                    Ok(
                        { new IDisposable with
                            member _.Dispose() = invokeDisposer subscription }
                    )
            with ex ->
                Error(sprintf "OPENCODE-SIGNAL-SUBSCRIBE: %s" ex.Message)

    let private subscribeGlobalEvent (client: obj) (onSignalEvent: obj -> unit) : Result<IDisposable, string> =
        if isNull client then
            Error "OPENCODE-SIGNAL-SUBSCRIBE: no client for global event"
        else
            let globalApi = client?``global``

            if isNull globalApi || isNull globalApi?event then
                Error "OPENCODE-SIGNAL-SUBSCRIBE: /global/event unavailable"
            else
                try
                    let abortCtrl = newAbortController ()

                    let onEvent =
                        box (fun data ->
                            let normalized = unwrap data

                            if isSignalEvent normalized then
                                onSignalEvent normalized)

                    let options =
                        createObj
                            [ "signal", abortCtrl?signal
                              "onSseEvent",
                              box (fun (evt: obj) ->
                                  if not (isNull evt) then
                                      let data = if isNull evt?data then evt else evt?data
                                      let normalized = unwrap data

                                      if isSignalEvent normalized then
                                          onSignalEvent normalized) ]

                    emitJsExpr
                        (globalApi, options, onEvent, abortCtrl)
                        """
                        ((globalApi, options, onEvent, abortCtrl) => {
                          (async () => {
                            try {
                              const result = await globalApi.event(options);
                              const stream = result && result.stream ? result.stream : result;
                              if (!stream || typeof stream[Symbol.asyncIterator] !== 'function') return;
                              for await (const data of stream) {
                                if (abortCtrl.signal.aborted) break;
                                onEvent(data);
                              }
                            } catch (_) {}
                          })();
                        })($0, $1, $2, $3)
                        """

                    Ok(
                        { new IDisposable with
                            member _.Dispose() =
                                try
                                    abortCtrl?abort ()
                                with _ ->
                                    () }
                    )
                with ex ->
                    Error(sprintf "OPENCODE-SIGNAL-SUBSCRIBE: %s" ex.Message)

    let trySubscribe (input: obj) (onSignalEvent: obj -> unit) : Result<IDisposable option * string, string> =
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

        let client = if isNull input then null else input?client

        // Host often emits non-retryable provider failures only on global SSE.
        // Keep idle/retry/deleted single-sourced; allow session.error from global
        // as a second, filtered subscription when local listen is primary.
        let onlySessionError (raw: obj) =
            if eventTypeOf (unwrap raw) = "session.error" then
                onSignalEvent raw

        match listenTarget with
        | Some events ->
            match subscribeListen events onSignalEvent with
            | Error err -> Error err
            | Ok localSub ->
                match subscribeGlobalEvent client onlySessionError with
                | Ok globalSub ->
                    logInfo "OPENCODE-SIGNAL-SOURCE" "events.listen+global.session.error"

                    let composite =
                        { new IDisposable with
                            member _.Dispose() =
                                localSub.Dispose()
                                globalSub.Dispose() }

                    Ok(Some composite, "events.listen+global.session.error")
                | Error _ ->
                    // Global optional: local alone still satisfies idle/retry/deleted.
                    logInfo "OPENCODE-SIGNAL-SOURCE" "events.listen"
                    Ok(Some localSub, "events.listen")
        | None ->
            match subscribeGlobalEvent client onSignalEvent with
            | Ok sub ->
                logInfo "OPENCODE-SIGNAL-SOURCE" "global.event"
                Ok(Some sub, "global.event")
            | Error err -> Error err
