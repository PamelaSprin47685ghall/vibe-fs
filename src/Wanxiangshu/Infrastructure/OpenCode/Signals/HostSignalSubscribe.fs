namespace Wanxiangshu.OpenCode

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

    let private subscribeListen (events: obj) (onSignalEvent: obj -> unit) : Result<IDisposable, string> =
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

                    let onEvent = box onSignalEvent

                    let options =
                        createObj [ "signal", abortCtrl?signal; "onSseEvent", box onSignalEvent ]

                    emitJsExpr
                        (globalApi, options, onEvent, abortCtrl)
                        """
                        ((globalApi, options, onEvent, abortCtrl) => {
                          (async () => {
                            let attempt = 0;
                            while (!abortCtrl.signal.aborted) {
                              try {
                                const result = await globalApi.event(options);
                                const stream = result && result.stream ? result.stream : result;
                                if (!stream || typeof stream[Symbol.asyncIterator] !== 'function') {
                                  console.info('OPENCODE-SIGNAL-SSE', 'stream unavailable');
                                } else {
                                  for await (const data of stream) {
                                    if (abortCtrl.signal.aborted) break;
                                    onEvent(data);
                                  }
                                  console.info('OPENCODE-SIGNAL-SSE', 'stream ended normally');
                                }
                              } catch (err) {
                                console.info('OPENCODE-SIGNAL-SSE', 'stream ended or failed: ' + (err && err.message ? err.message : String(err)));
                              }
                              if (abortCtrl.signal.aborted) break;
                              const delay = Math.min(1000 * 2 ** attempt, 10000);
                              await new Promise(r => setTimeout(r, delay));
                              attempt++;
                            }
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

        // Global SSE carries sessions from manager worktrees whose directory
        // differs from this plugin instance. Prefer it whenever available;
        // fall back to the directory-scoped listener for older hosts without
        // /global/event.
        match subscribeGlobalEvent client onSignalEvent with
        | Ok sub ->
            logInfo "OPENCODE-SIGNAL-SOURCE" "global.event"
            Ok(Some sub, "global.event")
        | Error globalError ->
            match listenTarget with
            | Some events ->
                match subscribeListen events onSignalEvent with
                | Error err -> Error err
                | Ok localSub ->
                    logInfo "OPENCODE-SIGNAL-SOURCE" "events.listen"
                    Ok(Some localSub, "events.listen")
            | None -> Error globalError
