namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop

/// Subscribes coarse host signals from exactly one source per plugin instance:
/// global SSE preferred; local events.listen as fallback for older hosts.
/// Both unavailable is a hard failure — no dual delivery, no silent degradation.
///
/// Global SSE: supervised reconnect + heartbeat. Half-open TCP (no error, no end)
/// is detected by silence timeout; errors surface via console.warn and Health.
module HostSignalSubscribe =

    /// Snapshot of the global SSE transport (or local listen stub).
    type SignalHealth =
        { IsConnected: bool
          LastEventReceived: DateTimeOffset option
          LastError: string option
          ReconnectAttempts: int }

    /// Disposable subscription plus a cheap health probe for downstream recovery.
    type HostSignalSubscription =
        { Health: unit -> SignalHealth
          Dispose: unit -> unit }

    /// Heartbeat interval: check silence every 15s.
    let private HeartbeatIntervalMs = 15_000

    /// No event within 30s → connection treated as dead; force reconnect.
    let private HeartbeatTimeoutMs = 30_000

    [<Emit("$0()")>]
    let private invokeDisposer (value: obj) : unit = jsNative

    [<Emit("console.info($0, $1)")>]
    let private logInfo (prefix: string) (message: string) : unit = jsNative

    let private alwaysHealthy () : SignalHealth =
        { IsConnected = true
          LastEventReceived = None
          LastError = None
          ReconnectAttempts = 0 }

    let private readHealth (state: obj) : SignalHealth =
        let disposed: bool = unbox state?disposed
        let connected: bool = unbox state?connected
        let lastMs: float = unbox state?lastEventMs
        let lastError: obj = state?lastError
        let attempts: int = unbox state?reconnectAttempts

        let lastEvent =
            if lastMs > 0.0 then
                Some(DateTimeOffset.FromUnixTimeMilliseconds(int64 lastMs))
            else
                None

        let error = if isNull lastError then None else Some(string lastError)

        { IsConnected = connected && not disposed
          LastEventReceived = lastEvent
          LastError = error
          ReconnectAttempts = attempts }

    let private disposeState (state: obj) : unit =
        if not (unbox state?disposed) then
            state?disposed <- true

            let timer = state?heartbeatTimer

            if not (isNull timer) then
                emitJsExpr timer "clearInterval($0)"
                state?heartbeatTimer <- null

            let connAbort = state?connAbort

            if not (isNull connAbort) then
                try
                    connAbort?abort ()
                with _ ->
                    ()

                state?connAbort <- null

            state?connected <- false

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

    let private subscribeGlobalEvent
        (client: obj)
        (onSignalEvent: obj -> unit)
        : Result<HostSignalSubscription, string> =
        if isNull client then
            Error "OPENCODE-SIGNAL-SUBSCRIBE: no client for global event"
        else
            let globalApi = client?``global``

            if isNull globalApi || isNull globalApi?event then
                Error "OPENCODE-SIGNAL-SUBSCRIBE: /global/event unavailable"
            else
                try
                    let onEvent = box onSignalEvent

                    // Mutable transport state shared with the emitJsExpr loop.
                    // lastEventMs seeded at subscribe time → 30s grace before first
                    // heartbeat timeout (no events yet is not immediately fatal).
                    let state =
                        createObj
                            [ "disposed", box false
                              "connected", box false
                              "lastEventMs", box (float (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
                              "lastError", box null
                              "reconnectAttempts", box 0
                              "connAbort", box null
                              "heartbeatTimer", box null ]

                    emitJsExpr
                        (globalApi, onEvent, state, HeartbeatIntervalMs, HeartbeatTimeoutMs)
                        """
                        ((globalApi, onEvent, state, heartbeatMs, timeoutMs) => {
                          (async () => {
                            let attempt = 0;
                            while (!state.disposed) {
                              const connAbort = new AbortController();
                              state.connAbort = connAbort;
                              state.connected = true;
                              // Grace period for this connection: silence clock starts now.
                              state.lastEventMs = Date.now();
                              const options = { signal: connAbort.signal, onSseEvent: onEvent };
                              try {
                                const result = await globalApi.event(options);
                                const stream = result && result.stream ? result.stream : result;
                                if (!stream || typeof stream[Symbol.asyncIterator] !== 'function') {
                                  state.lastError = 'stream unavailable';
                                  console.warn('OPENCODE-SIGNAL-SSE', 'stream unavailable');
                                } else {
                                  for await (const data of stream) {
                                    if (state.disposed || connAbort.signal.aborted) break;
                                    state.lastEventMs = Date.now();
                                    state.lastError = null;
                                    onEvent(data);
                                  }
                                  if (!state.disposed && !connAbort.signal.aborted) {
                                    console.info('OPENCODE-SIGNAL-SSE', 'stream ended normally');
                                  }
                                }
                              } catch (err) {
                                // Preserve heartbeat timeout reason if we forced abort.
                                if (!state.lastError) {
                                  state.lastError = err && err.message ? err.message : String(err);
                                }
                                if (!state.disposed) {
                                  console.warn('OPENCODE-SIGNAL-SSE', 'stream ended or failed: ' + state.lastError);
                                }
                              }
                              state.connected = false;
                              state.connAbort = null;
                              if (state.disposed) break;
                              const delay = Math.min(1000 * 2 ** attempt, 10000);
                              await new Promise(r => setTimeout(r, delay));
                              if (state.disposed) break;
                              attempt++;
                              state.reconnectAttempts = attempt;
                            }
                          })();

                          const hb = setInterval(() => {
                            if (state.disposed) return;
                            const silent = Date.now() - state.lastEventMs;
                            if (silent > timeoutMs && state.connAbort && !state.connAbort.signal.aborted) {
                              const msg = 'heartbeat timeout after ' + silent + 'ms';
                              state.lastError = msg;
                              console.warn('OPENCODE-SIGNAL-SSE', msg);
                              try { state.connAbort.abort(); } catch (_) {}
                            }
                          }, heartbeatMs);
                          if (typeof hb.unref === 'function') hb.unref();
                          state.heartbeatTimer = hb;
                        })($0, $1, $2, $3, $4)
                        """

                    Ok
                        { Health = fun () -> readHealth state
                          Dispose = fun () -> disposeState state }
                with ex ->
                    Error(sprintf "OPENCODE-SIGNAL-SUBSCRIBE: %s" ex.Message)

    let trySubscribe
        (input: obj)
        (onSignalEvent: obj -> unit)
        : Result<HostSignalSubscription option * string, string> =
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
