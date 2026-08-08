namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Process

    /// Subscribes coarse host signals from exactly one source per plugin instance:
    /// local events.listen preferred; global SSE only when the local listener is
    /// unavailable. Both unavailable is a hard failure — no dual delivery, no
    /// silent degradation, and never a cross-instance connection.
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

    /// No event within 30s → connection treated as dead (one-shot silence deadline).
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

    let private disposeState (state: obj) (port: ITimerPort) : unit =
        if not (unbox state?disposed) then
            state?disposed <- true

            // Cancel heartbeat + reconnect handles, unblock reconnect await, dispose port.
            emitJsExpr
                state
                """
                ((state) => {
                  if (state.heartbeatHandle) {
                    state.heartbeatHandle.Cancel();
                    state.heartbeatHandle = null;
                  }
                  if (state.reconnectHandle) {
                    state.reconnectHandle.Cancel();
                    state.reconnectHandle = null;
                  }
                  if (state.reconnectResolve) {
                    const resolve = state.reconnectResolve;
                    state.reconnectResolve = null;
                    resolve();
                  }
                })($0)
                """

            port.Dispose()

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

    /// A silent SSE link is an out-of-contract signal channel: reconnecting in
    /// a loop cannot restore a half-open connection and only manufactures
    /// noise, so one timeout kills the process via Diagnostic.fatal (SIGKILL).
    /// TUI-embedded mode never subscribes (the probe refuses), so a timeout
    /// here means a genuinely dead link, not a misconfiguration.
    let private onHeartbeatTimeout (silentMs: int) : unit =
        Diagnostic.fatal "sse-heartbeat-timeout" [ "duration", string silentMs ]

    let private subscribeGlobalEvent
        (client: obj)
        (onSignalEvent: obj -> unit)
        (timerPort: ITimerPort option)
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
                    // Timers via ITimerPort injection (prod=nodeTimerPort, test=virtualTimerPort).
                    let port = defaultArg timerPort (PtyTiming.nodeTimerPort ())

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
                              "heartbeatHandle", box null
                              "reconnectHandle", box null
                              "reconnectResolve", box null ]

                    // One-shot silence deadline (VERIFY-004 / C class): each event
                    // Cancels + re-arms port.Delay(timeoutMs). No period scan.
                    // Heartbeat + reconnect timers via ITimerPort injection
                    // (production=nodeTimerPort, test=virtualTimerPort).
                    emitJsExpr
                        (globalApi, onEvent, state, HeartbeatTimeoutMs, onHeartbeatTimeout, port)
                        """
                        ((globalApi, onEvent, state, timeoutMs, onHeartbeatTimeout, port) => {
                          const armHeartbeat = () => {
                            if (state.disposed) return;
                            if (state.heartbeatHandle) {
                              state.heartbeatHandle.Cancel();
                              state.heartbeatHandle = null;
                            }
                            const handle = port.Delay(timeoutMs);
                            state.heartbeatHandle = handle;
                            // handle.Delay is a getter (Task/Promise), not a method.
                            handle.Delay.then(() => {
                              if (state.disposed) return;
                              const silent = Date.now() - state.lastEventMs;
                              // Heartbeat is the causal watchdog for this SSE link: a
                              // half-open TCP connection emits neither close/error nor
                              // bytes. Detection is not a retry signal — a dead link
                              // cannot be reconnected into existence, and a reconnect
                              // loop over a dead link only manufactures noise. One
                              // timeout reports and kills the process (Diagnostic.fatal);
                              // Cancel + nulling prevents re-fire.
                              onHeartbeatTimeout(silent);
                              state.heartbeatHandle = null;
                            });
                          };

                          // Initial seed: 30s grace after subscribe with no events.
                          armHeartbeat();

                          (async () => {
                            let attempt = 0;
                            while (!state.disposed) {
                              const connAbort = new AbortController();
                              state.connAbort = connAbort;
                              state.connected = true;
                              // Grace period for this connection: silence clock starts now.
                              state.lastEventMs = Date.now();
                              armHeartbeat();
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
                                    armHeartbeat();
                                    onEvent(data);
                                  }
                                  if (!state.disposed && !connAbort.signal.aborted) {
                                    console.info('OPENCODE-SIGNAL-SSE', 'stream ended normally');
                                  }
                                }
                              } catch (err) {
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
                              // ITimerPort backoff; Cancel + reconnectResolve unblocks dispose mid-wait
                              // (Cancel alone leaves Delay pending forever by contract).
                              const h = port.Delay(delay);
                              state.reconnectHandle = h;
                              await new Promise((resolve) => {
                                state.reconnectResolve = resolve;
                                h.Delay.then(() => {
                                  if (state.reconnectResolve === resolve) {
                                    state.reconnectResolve = null;
                                    resolve();
                                  }
                                });
                              });
                              state.reconnectHandle = null;
                              if (state.disposed) break;
                              attempt++;
                              state.reconnectAttempts = attempt;
                            }
                          })();
                        })($0, $1, $2, $3, $4, $5)
                        """

                    Ok
                        { Health = fun () -> readHealth state
                          Dispose = fun () -> disposeState state port }
                with ex ->
                    Error(sprintf "OPENCODE-SIGNAL-SUBSCRIBE: %s" ex.Message)

    let private serverUrlOf (input: obj) : string option =
        if isNull input then
            None
        else
            let value: obj = input?serverUrl

            if isNull value then
                None
            else
                let url = string value

                if String.IsNullOrWhiteSpace url then None else Some url

    /// Probe timeout for the SSE reachability check. A dead fallback port
    /// refuses instantly (ECONNREFUSED); this bound only covers a half-open
    /// network that silently drops the connection.
    let private ServerProbeTimeoutMs = 3_000

    /// Probes whether a real HTTP listener answers behind `serverUrl`.
    ///
    /// The SDK's `global.event` streams through the GLOBAL fetch — never the
    /// in-process custom fetch the Host injects for every other client call
    /// (`../opencode/packages/sdk/js/src/gen/core/serverSentEvents.gen.ts`) — so
    /// in embedded (TUI) mode, where `Server.url` stays undefined and the
    /// plugin input carries the Host's dead fallback address, a subscription
    /// connects to nothing and the SDK retries forever without yielding; the
    /// heartbeat watchdog then trips a reconnect every ~45s. The one reliable
    /// discriminator is a live request: a real OpenCode listener answers the health
    /// endpoint with a valid health JSON payload (`healthy: true` or numeric `pid`);
    /// random non-OpenCode servers or dead fallbacks refuse.
    [<Emit("""((url, timeoutMs) => new Promise((resolve) => {
          const check = (path) => new Promise((res) => {
            const c = new AbortController();
            const t = setTimeout(() => { try { c.abort(); } catch {} }, timeoutMs);
            let target = null;
            try { target = new URL(path, url).toString(); } catch {}
            if (target === null) { clearTimeout(t); res(false); return; }
            fetch(target, { signal: c.signal, method: 'GET' })
              .then((r) => {
                clearTimeout(t);
                if (!r.ok) { res(false); return; }
                return r.json()
                  .then((data) => {
                    const valid = data && (data.healthy === true || typeof data.pid === 'number');
                    res(Boolean(valid));
                  })
                  .catch(() => res(false));
              })
              .catch(() => { clearTimeout(t); res(false); });
          });
          check('/api/health').then((ok1) => {
            if (ok1) resolve(true);
            else check('/global/health').then((ok2) => resolve(ok2));
          });
        }))($0, $1)""")>]
    let private probeServer (url: string) (timeoutMs: int) : Task<bool> = jsNative

    let trySubscribe
        (input: obj)
        (onSignalEvent: obj -> unit)
        (timerPort: ITimerPort option)
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

            let client = if isNull input then null else input?client

            // Directory-scoped listener is authoritative for this plugin
            // instance; the global SSE transport is only a fallback when the
            // local listener is unavailable. This ordering prevents subscribing
            // to a foreign instance's sessions. Hard-fail only when no transport
            // remains at all.
            let subscribeGlobalOrLocal () =
                match listenTarget with
                | Some events ->
                    match subscribeListen events onSignalEvent with
                    | Ok localSub ->
                        logInfo "OPENCODE-SIGNAL-SOURCE" "events.listen"
                        Ok(Some localSub, "events.listen")
                    | Error localError ->
                        match subscribeGlobalEvent client onSignalEvent timerPort with
                        | Ok sub ->
                            logInfo "OPENCODE-SIGNAL-SOURCE" "global.event"
                            Ok(Some sub, "global.event")
                        | Error _ -> Error localError
                | None ->
                    match subscribeGlobalEvent client onSignalEvent timerPort with
                    | Ok sub ->
                        logInfo "OPENCODE-SIGNAL-SOURCE" "global.event"
                        Ok(Some sub, "global.event")
                    | Error globalError -> Error globalError

            // Embedded (TUI) mode: no listener answers the server URL, so the
            // SDK SSE path cannot deliver. Local signals already arrive through
            // the Host `event` hook (ObserveLocal); the legacy events.listen is
            // a bonus, not a requirement — degrade silently.
            let subscribeListenOrDegrade (sourceLabel: string) =
                match listenTarget with
                | Some events ->
                    match subscribeListen events onSignalEvent with
                    | Ok localSub ->
                        logInfo "OPENCODE-SIGNAL-SOURCE" "events.listen"
                        Ok(Some localSub, "events.listen")
                    | Error _ ->
                        logInfo "OPENCODE-SIGNAL-SOURCE" sourceLabel
                        Ok(None, sourceLabel)
                | None ->
                    logInfo "OPENCODE-SIGNAL-SOURCE" sourceLabel
                    Ok(None, sourceLabel)

            match serverUrlOf input with
            | None ->
                // Legacy hosts without the serverUrl field keep the legacy
                // verdict (hard error when no transport remains).
                return subscribeGlobalOrLocal ()
            | Some url ->
                let! reachable = probeServer url ServerProbeTimeoutMs

                if reachable then
                    return subscribeGlobalOrLocal ()
                else
                    return subscribeListenOrDegrade "local-event-hook"
        }
