namespace Wanxiangshu.Process

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Repository.Programming.Js

/// JS-011/JS-053 sandbox: arbitrary model JavaScript runs in a Node vm
/// context created with DONT_CONTEXTIFY. Host values (api returns, errors,
/// promises, Callable instances) cross a membrane into sandbox-realm copies
/// so `constructor.constructor` cannot reach the real Node process. Transfer
/// globals are deleted before user code runs; the deadline is checked with
/// the host clock inside the membrane, not a forgeable sandbox Date.now.
module JsSandbox =

    /// Park lifecycle state carried by the api closure. The park member reads
    /// and writes these fields through the same mutable object the workflow
    /// holds, so a parked program and the orchestrator observe one state.
    ///
    /// `ParkSignalResolve` is the park-signal resolver the workflow installs
    /// before each race: the park member calls it with the payload when the
    /// program suspends, completing the signal the workflow races against the
    /// program promise. `ParkDeferredResolve` / `ParkDeferredReject` are the
    /// deferred-promise resolvers the park member creates: `unpark` calls
    /// resolve with the injected value (the program continues as if the await
    /// returned it); `abandon` calls reject with the `__jsAbandoned` marker so
    /// native try/finally runs. `AbandonRequested` marks the cleanup window
    /// during which park is refused.
    type ParkState =
        {
            mutable ParkRequested: bool
            mutable ParkPayload: obj
            /// The park-signal resolver (a JS function) installed before each
            /// race; the park member calls it with the payload to complete the
            /// signal the workflow races against. `null` before install.
            mutable ParkSignalResolve: obj
            /// The deferred-promise resolver the park member creates; `unpark`
            /// calls it with the injected value. `null` while not parked.
            mutable ParkDeferredResolve: obj
            /// The deferred-promise rejecter the park member creates; `abandon`
            /// calls it with the `__jsAbandoned` marker. `null` while not parked.
            mutable ParkDeferredReject: obj
            /// The JS stack captured when the park member fired. The workflow
            /// uses it to mark the model-source line that is currently
            /// suspended; null when no stack was captured.
            mutable ParkStack: string
            mutable AbandonRequested: bool
        }

    /// Handle to a started sandbox program: the async IIFE promise is exposed
    /// (not awaited to completion) so a caller may hold it across calls, and
    /// the deadline reference is mutable so a caller may reset the async
    /// deadline without re-wrapping the program.
    type JsRunHandle =
        { ProgramPromise: Task<string>
          DeadlineRef: obj
          Api: obj
          ParkState: ParkState }

    [<Import("Buffer", "node:buffer")>]
    let private nodeBuffer: obj = jsNative

    [<Import("createContext", "node:vm")>]
    let private createContext (sandbox: obj) (options: obj) : obj = jsNative

    [<Import("runInContext", "node:vm")>]
    let private runInContext (code: string) (context: obj) (options: obj) : obj = jsNative

    [<Import("constants", "node:vm")>]
    let private vmConstants: obj = jsNative

    [<Emit("JSON.parse($0)")>]
    let private parseJson (text: string) : obj = jsNative

    [<Emit("$0.current = Number($1)")>]
    let private setDeadline (deadlineRef: obj) (epochMs: int64) : unit = jsNative

    /// Membrane + DONT_CONTEXTIFY bootstrap (see cards/CUT-25.md). Host values
    /// cross the boundary only through wrap/unwrap; transfer globals are deleted
    /// before user code runs.
    let private sandboxInstallSource =
        """
(function () {
  "use strict";
  var Object = globalThis.Object;
  var Array = globalThis.Array;
  var Promise = globalThis.Promise;
  var Error = globalThis.Error;
  var Proxy = globalThis.Proxy;
  var Reflect = globalThis.Reflect;
  var WeakMap = globalThis.WeakMap;
  var String = globalThis.String;
  var JSON = globalThis.JSON;
  var isArray = Array.isArray;
  var getOwnNames = Object.getOwnPropertyNames;
  var slot = null;
  globalThis.__put = function (v) { slot = v; };
  globalThis.__take = function () { var s = slot; slot = null; return s; };
  globalThis.__makeRunner = function (hostApi, hostDeadlineRef, hostNow, baseClassSource, modelSource) {
    var hostToGuest = new WeakMap();
    var guestToHost = new WeakMap();
    function isPrimitive(v) {
      return v === null || (typeof v !== "object" && typeof v !== "function");
    }
    function copyError(hostErr) {
      var g = new Error(String(hostErr && hostErr.message != null ? hostErr.message : hostErr));
      if (hostErr && typeof hostErr.name === "string") g.name = hostErr.name;
      if (hostErr && hostErr.__jsFailure && typeof hostErr.__jsFailure === "object") {
        g.__jsFailure = {
          code: String(hostErr.__jsFailure.code || ""),
          reason: String(hostErr.__jsFailure.reason || hostErr.__jsFailure.code || ""),
        };
      }
      if (hostErr && hostErr.__jsAbandoned) g.__jsAbandoned = true;
      if (hostErr && hostErr.__jsDelivered) g.__jsDelivered = wrap(hostErr.__jsDelivered);
      return g;
    }
    function looksLikeError(v) {
      return (
        v &&
        typeof v === "object" &&
        typeof v.message === "string" &&
        (typeof v.name === "string" || v.__jsFailure || v.__jsAbandoned || v.__jsDelivered)
      );
    }
    function bridgeInstance(guestInst) {
      if (guestToHost.has(guestInst)) return guestToHost.get(guestInst);
      var proxy = new Proxy(guestInst, {
        get: function (target, prop) {
          if (prop === "constructor") return undefined;
          var v = Reflect.get(target, prop, target);
          if (typeof v !== "function") {
            if (isPrimitive(v)) return v;
            return unwrap(v);
          }
          return function () {
            var hostArgs = Array.prototype.slice.call(arguments);
            var guestArgs = hostArgs.map(wrap);
            try {
              return unwrap(Reflect.apply(v, target, guestArgs));
            } catch (e) {
              throw unwrap(e);
            }
          };
        },
        set: function () { return false; },
        defineProperty: function () { return false; },
        deleteProperty: function () { return false; },
        getPrototypeOf: function () { return null; },
      });
      guestToHost.set(guestInst, proxy);
      hostToGuest.set(proxy, guestInst);
      return proxy;
    }
    function wrap(hostValue) {
      if (isPrimitive(hostValue)) return hostValue;
      if (hostToGuest.has(hostValue)) return hostToGuest.get(hostValue);
      if (looksLikeError(hostValue)) {
        var ge = copyError(hostValue);
        hostToGuest.set(hostValue, ge);
        guestToHost.set(ge, hostValue);
        return ge;
      }
      if (typeof hostValue.then === "function") {
        var gp = new Promise(function (resolve, reject) {
          hostValue.then(
            function (v) { resolve(wrap(v)); },
            function (e) { reject(wrap(e)); }
          );
        });
        hostToGuest.set(hostValue, gp);
        guestToHost.set(gp, hostValue);
        return gp;
      }
      if (typeof hostValue === "function") {
        var guestFn = function () {
          if (hostNow() > Number(hostDeadlineRef.current)) {
            var err = new Error("program exceeded its deadline");
            err.__jsFailure = { code: "PROGRAM_TIMEOUT", reason: "program exceeded its deadline" };
            throw err;
          }
          var guestArgs = Array.prototype.slice.call(arguments);
          var hostArgs = guestArgs.map(unwrap);
          try {
            return wrap(Reflect.apply(hostValue, unwrap(this), hostArgs));
          } catch (e) {
            throw wrap(e);
          }
        };
        hostToGuest.set(hostValue, guestFn);
        guestToHost.set(guestFn, hostValue);
        return guestFn;
      }
      if (isArray(hostValue)) {
        var ga = [];
        hostToGuest.set(hostValue, ga);
        guestToHost.set(ga, hostValue);
        for (var i = 0; i < hostValue.length; i++) ga[i] = wrap(hostValue[i]);
        return ga;
      }
      var go = {};
      hostToGuest.set(hostValue, go);
      guestToHost.set(go, hostValue);
      var names = getOwnNames(hostValue);
      for (var j = 0; j < names.length; j++) {
        var k = names[j];
        try { go[k] = wrap(hostValue[k]); } catch (_e) {}
      }
      return go;
    }
    function unwrap(guestValue) {
      if (isPrimitive(guestValue)) return guestValue;
      if (guestToHost.has(guestValue)) return guestToHost.get(guestValue);
      if (typeof guestValue === "function") {
        var hostFn = function () {
          var hostArgs = Array.prototype.slice.call(arguments);
          var guestArgs = hostArgs.map(wrap);
          try {
            if (new.target) {
              var inst = Reflect.construct(guestValue, guestArgs, guestValue);
              return bridgeInstance(inst);
            }
            return unwrap(Reflect.apply(guestValue, wrap(this), guestArgs));
          } catch (e) {
            throw unwrap(e);
          }
        };
        guestToHost.set(guestValue, hostFn);
        hostToGuest.set(hostFn, guestValue);
        return hostFn;
      }
      if (typeof guestValue.then === "function") {
        var hp = new Promise(function (resolve, reject) {
          guestValue.then(
            function (v) { resolve(unwrap(v)); },
            function (e) { reject(unwrap(e)); }
          );
        });
        guestToHost.set(guestValue, hp);
        hostToGuest.set(hp, guestValue);
        return hp;
      }
      if (isArray(guestValue)) {
        var ha = [];
        guestToHost.set(guestValue, ha);
        hostToGuest.set(ha, guestValue);
        for (var i2 = 0; i2 < guestValue.length; i2++) ha[i2] = unwrap(guestValue[i2]);
        return ha;
      }
      var ho = {};
      guestToHost.set(guestValue, ho);
      hostToGuest.set(ho, guestValue);
      var gnames = getOwnNames(guestValue);
      for (var j2 = 0; j2 < gnames.length; j2++) {
        var gk = gnames[j2];
        try { ho[gk] = unwrap(guestValue[gk]); } catch (_e2) {}
      }
      return ho;
    }
    var api = wrap(hostApi);
    var body =
      "(async () => {\n" +
      baseClassSource +
      "\n" +
      modelSource +
      "\n" +
      "let __result;\n" +
      "try { __result = await new Js(api).run(); }\n" +
      "catch (__e) {\n" +
      "  if (__e && __e.__jsFailure && typeof __e.__jsFailure.code === 'string') {\n" +
      "    return JSON.stringify({\n" +
      "      __jsHostFailed: true,\n" +
      "      code: __e.__jsFailure.code,\n" +
      "      reason: String(__e.__jsFailure.reason || __e.__jsFailure.code)\n" +
      "    });\n" +
      "  }\n" +
      "  if (__e && __e.__jsAbandoned) {\n" +
      "    return JSON.stringify({ __jsAbandoned: true });\n" +
      "  }\n" +
      "  if (__e && __e.__jsDelivered) {\n" +
      "    return JSON.stringify({ __jsDelivered: true, view: __e.__jsDelivered.view });\n" +
      "  }\n" +
      "  return JSON.stringify({ __jsProgramFailed: true, message: String((__e && __e.message) || __e) });\n" +
      "}\n" +
      "try {\n" +
      "  const __json = JSON.stringify(__result);\n" +
      "  return typeof __json === 'string' ? __json : JSON.stringify({ __jsInvalidReturn: true });\n" +
      "}\n" +
      "catch (__e2) { return JSON.stringify({ __jsInvalidReturn: true }); }\n" +
      "})()";
    var runBody = new Function("api", "JSON", "return (" + body + ");");
    return runBody(api, JSON);
  };
})();
"""

    let private launchMembraned
        (api: obj)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (baseClassSource: string)
        (modelSource: string)
        : obj =
        emitJsExpr
            (vmConstants, createContext, runInContext, sandboxInstallSource, api, deadlineMs, deadlineEpochMs, baseClassSource, modelSource)
            """
(function(vmConstants, createContext, runInContext, INSTALL, api, deadlineMs, deadlineEpochMs, baseClass, model) {
  var ctx = createContext(vmConstants.DONT_CONTEXTIFY, { microtaskMode: "afterEvaluate" });
  runInContext(INSTALL, ctx, { timeout: 5000 });
  var deadlineRef = { current: Number(deadlineEpochMs) };
  var hostNow = function () { return Date.now(); };
  var put = runInContext("__put", ctx);
  put({ api: api, deadlineRef: deadlineRef, hostNow: hostNow, base: baseClass, model: model });
  var progPromise;
  try {
    progPromise = runInContext(
      "(function(){ var t = __take(); var make = __makeRunner; delete globalThis.__put; delete globalThis.__take; delete globalThis.__makeRunner; return make(t.api, t.deadlineRef, t.hostNow, t.base, t.model); })()",
      ctx,
      { timeout: deadlineMs }
    );
  } catch (e) {
    return { DeadlineRef: deadlineRef, ProgramPromise: Promise.reject(e) };
  }
  var box = { done: false, value: null, error: null, failed: false };
  var register = runInContext(
    "(function(p, box){ Promise.resolve(p).then(function(v){ box.done = true; box.value = v; }, function(e){ box.done = true; box.failed = true; box.error = e; }); })",
    ctx
  );
  register(progPromise, box);
  var driven = (async function () {
    while (!box.done) {
      var now = Date.now();
      var deadlineNow = Number(deadlineRef.current);
      if (now > deadlineNow) {
        var err = new Error("Script execution timed out");
        err.code = "ERR_SCRIPT_EXECUTION_TIMEOUT";
        throw err;
      }
      var remaining = Math.max(1, Math.min(Number(deadlineMs), deadlineNow - now));
      try {
        runInContext("void 0", ctx, { timeout: remaining });
      } catch (e2) {
        if (e2 && e2.code === "ERR_SCRIPT_EXECUTION_TIMEOUT") throw e2;
        throw e2;
      }
      if (box.done) break;
      await new Promise(function (r) { setImmediate(r); });
    }
    if (box.failed) throw box.error;
    return box.value;
  })();
  return { DeadlineRef: deadlineRef, ProgramPromise: driven };
})($0, $1, $2, $3, $4, $5, $6, $7, $8)
"""


    /// Sentinel payloads the wrapper returns instead of the program result;
    /// recognized by prefix, never by exception-message sniffing (JS-019).
    let private failedPrefix = "{\"__jsProgramFailed\":"
    let private hostFailedPrefix = "{\"__jsHostFailed\":"
    let private invalidReturnPrefix = "{\"__jsInvalidReturn\":"
    let private parkedPrefix = "{\"__jsParked\":"
    let private deliveredPrefix = "{\"__jsDelivered\":"

    /// The ABANDONED protocol sentinel. Rejecting the program promise with an
    /// error carrying this marker unwinds the program stack from its await
    /// point so native try/finally blocks run as a finalizer (PLAN S2 step 6 /
    /// algorithm F; Python GeneratorExit precedent). The marker is a stable
    /// prefix, not a message-text sniff.
    let abandonedSentinel = "__jsAbandoned"

    /// Wrap base class + model source into an async IIFE that evaluates to a
    /// JSON string. The api is proxied so every member call re-checks the
    /// mutable injected deadline reference: synchronous segments are bounded by
    /// the vm timeout, async segments by the proxy (JS-054.1). The deadline is
    /// read from the vm-global `__deadlineRef.current` at each call, so a
    /// caller may reset it between calls without re-wrapping the program.
    ///
    /// Park lifecycle: the api's `park(payload)` member (built by
    /// ToolsBindings) sets `__parkState.ParkRequested`, fires the park signal
    /// the workflow races against, and returns a deferred Promise the program
    /// awaits. The wrapper itself is a plain async IIFE: it stays pending while
    /// the program is suspended at `await this.park`, and resolves with the final
    /// result when the program finishes (or with a failure/timeout sentinel).
    /// `unpark` resolves the deferred with the injected value, letting the
    /// program continue as if the await returned that value.
    ///
    /// The abandon path rejects the program promise with an `__jsAbandoned`
    /// marker so native try/finally blocks run as a finalizer (PLAN S2 step 6).
    let wrapProgram (baseClassSource: string) (modelSource: string) : string =
        "(async () => {\n"
        + baseClassSource
        + "\n"
        + modelSource
        + "\n"
        + "const __wrap = (target) => new Proxy(target, {\n"
        + "  get(target, prop) {\n"
        + "    const value = target[prop];\n"
        + "    if (typeof value === 'function') {\n"
        + "      return (...args) => {\n"
        + "        if (Date.now() > __deadlineRef.current) {\n"
        + "          const err = new Error('program exceeded its deadline');\n"
        + "          err.__jsFailure = { code: 'PROGRAM_TIMEOUT', reason: 'program exceeded its deadline' };\n"
        + "          throw err;\n"
        + "        }\n"
        + "        return value(...args);\n"
        + "      };\n"
        + "    }\n"
        + "    if (value && typeof value === 'object') return __wrap(value);\n"
        + "    return value;\n"
        + "  }\n"
        + "});\n"
        + "const __api = __wrap(api);\n"
        + "let __result;\n"
        + "try { __result = await new Js(__api).run(); }\n"
        + "catch (__e) {\n"
        + "  if (__e && __e.__jsFailure && typeof __e.__jsFailure.code === 'string') {\n"
        + "    return JSON.stringify({\n"
        + "      __jsHostFailed: true,\n"
        + "      code: __e.__jsFailure.code,\n"
        + "      reason: String(__e.__jsFailure.reason || __e.__jsFailure.code)\n"
        + "    });\n"
        + "  }\n"
        + "  if (__e && __e.__jsAbandoned) {\n"
        + "    return JSON.stringify({ __jsAbandoned: true });\n"
        + "  }\n"
        + "  if (__e && __e.__jsDelivered) {\n"
        + "    return JSON.stringify({ __jsDelivered: true, view: __e.__jsDelivered.view });\n"
        + "  }\n"
        + "  return JSON.stringify({ __jsProgramFailed: true, message: String((__e && __e.message) || __e) });\n"
        + "}\n"
        + "try {\n"
        + "  const __json = JSON.stringify(__result);\n"
        + "  return typeof __json === 'string' ? __json : JSON.stringify({ __jsInvalidReturn: true });\n"
        + "}\n"
        + "catch (__e2) { return JSON.stringify({ __jsInvalidReturn: true }); }\n"
        + "})()"

    /// Classify a synchronous vm error: SyntaxError → INVALID_PROGRAM;
    /// vm timeout → PROGRAM_TIMEOUT; anything else → PROGRAM_FAILED.
    let private classifySyncError (error: obj) : JsFailure =
        let name = string (error?name)
        let message = string (error?message)

        if name = "SyntaxError" then
            JsFailure.InvalidProgram
        elif message.Contains "timed out" then
            JsFailure.ProgramTimeout
        else
            JsFailure.ProgramFailed message

    let private decodeHostFailed (json: string) : JsFailure =
        try
            let payload = parseJson json
            JsFailure.ofWire (string payload?code) (string payload?reason)
        with _ ->
            JsFailure.ProgramFailed "program threw"

    let private decodeProgramFailed (json: string) : JsFailure =
        try
            let payload = parseJson json
            JsFailure.ProgramFailed(string payload?message)
        with _ ->
            JsFailure.ProgramFailed "program threw"

    /// Decode the wrapper's JSON string into a typed result. `outputBoundBytes`
    /// bounds the serialized result (JS-054.2). Sentinel prefixes are matched
    /// before the byte bound so failures are never misreported as oversized.
    /// A parked sentinel is a normal outcome (the program suspended at
    /// `await this.park`), not a failure; it decodes to `Ok` with the sentinel
    /// JSON so the workflow can register the site.
    let private decodeRunResult (json: string) (outputBoundBytes: int) : Result<string, JsFailure> =
        if json.StartsWith hostFailedPrefix then
            Error(decodeHostFailed json)
        elif json.StartsWith failedPrefix then
            Error(decodeProgramFailed json)
        elif json.StartsWith invalidReturnPrefix then
            Error JsFailure.InvalidReturnValue
        elif json.StartsWith deliveredPrefix then
            Ok json
        elif json.StartsWith parkedPrefix then
            Ok json
        elif (nodeBuffer?byteLength (json, "utf8") |> unbox<int>) > outputBoundBytes then
            Error(JsFailure.ResultTooLarge None)
        else
            Ok json

    /// Start a wrapped program with an explicit park lifecycle state. The
    /// caller owns the ParkState and builds the api with park support
    /// (JsToolsBindings.createApiWithPark); this function injects both the api
    /// and the park state into the vm context.
    let runSurfaceWithPark
        (baseClassSource: string)
        (modelSource: string)
        (api: obj)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (parkState: ParkState option)
        : Task<JsRunHandle> =
        task {
            let state =
                match parkState with
                | Some state -> state
                | None ->
                    { ParkRequested = false
                      ParkPayload = null
                      ParkSignalResolve = null
                      ParkDeferredResolve = null
                      ParkDeferredReject = null
                      ParkStack = null
                      AbandonRequested = false }

            // Membrane launch: no host objects on the context global object;
            // ProgramPromise is a host-driven promise that drains afterEvaluate
            // microtasks so post-await sync loops still hit the vm timeout.
            let launched =
                launchMembraned api deadlineMs deadlineEpochMs baseClassSource modelSource

            let deadlineRef: obj = launched?DeadlineRef

            let promise: Task<string> =
                emitJsExpr (launched) "Promise.resolve($0.ProgramPromise)"

            return
                { ProgramPromise = promise
                  DeadlineRef = deadlineRef
                  Api = api
                  ParkState = state }
        }

    /// Start a wrapped program in a fresh vm context without awaiting it to
    /// completion. The async IIFE's promise is exposed as `ProgramPromise` so a
    /// caller may hold it across calls; the deadline is injected as a mutable
    /// reference (`DeadlineRef`) so a caller may reset the async deadline
    /// without re-wrapping. The vm context is held by the promise's closure
    /// chain while it remains unsettled — it is not explicitly lifted.
    ///
    /// `deadlineMs` bounds the synchronous segment via the vm timeout; the
    /// injected proxy bounds async segments (JS-054.1). Synchronous errors
    /// (SyntaxError, vm timeout) surface as a rejected `ProgramPromise` and are
    /// classified by `awaitResult`.
    let runSurface
        (baseClassSource: string)
        (modelSource: string)
        (api: obj)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        : Task<JsRunHandle> =
        runSurfaceWithPark baseClassSource modelSource api deadlineMs deadlineEpochMs None

    /// Await a started sandbox program and classify its outcome. Synchronous
    /// errors (SyntaxError → INVALID_PROGRAM, vm timeout → PROGRAM_TIMEOUT) are
    /// classified here; async failures arrive as sentinel JSON and are decoded
    /// by `decodeRunResult`.
    let awaitResult (handle: JsRunHandle) (outputBoundBytes: int) : Task<Result<string, JsFailure>> =
        task {
            try
                let! json = handle.ProgramPromise
                return decodeRunResult json outputBoundBytes
            with ex ->
                return Error(classifySyncError ex)
        }

    /// Classify one already-awaited wrapper JSON string (sentinel prefixes and
    /// output bound) without re-awaiting the program promise. The park race
    /// uses this so a program failure on the raced path decodes exactly like
    /// the plain path.
    let classifyResult (json: string) (outputBoundBytes: int) : Result<string, JsFailure> =
        decodeRunResult json outputBoundBytes

    /// Reset the async deadline of an in-flight sandbox program. Subsequent api
    /// member calls re-check `deadlineRef.current`, so the new epoch takes
    /// effect without re-wrapping or re-running the program.
    let resetDeadline (handle: JsRunHandle) (deadlineEpochMs: int64) : unit =
        setDeadline handle.DeadlineRef deadlineEpochMs

    let private extractFrameDigits (frame: string) : string option =
        let marker = "evalmachine.<anonymous>:"
        let at = frame.IndexOf marker

        if at < 0 then
            None
        else
            let rest = frame.Substring(at + marker.Length)
            let colon = rest.IndexOf ':'
            Some(if colon >= 0 then rest.Substring(0, colon) else rest)

    let private parseModelLine (modelStartLine: int) (modelEndLine: int) (digits: string) : int option =
        match System.Int32.TryParse digits with
        | true, line when line >= modelStartLine && line <= modelEndLine -> Some(line - modelStartLine)
        | _ -> None

    let private tryResolveFrameLine (modelStartLine: int) (modelEndLine: int) (frame: string) : int option =
        extractFrameDigits frame
        |> Option.bind (parseModelLine modelStartLine modelEndLine)

    let private locateSuspendedLine (modelStartLine: int) (modelEndLine: int) (stack: string) : int =
        if isNull stack then
            -1
        else
            stack.Split('\n')
            |> Array.tryPick (tryResolveFrameLine modelStartLine modelEndLine)
            |> Option.defaultValue -1

    /// Render the full model program with the suspended line marked. The
    /// wrapper places line 1 at `(async () => {`, the generated base class
    /// next, then the model source; `modelStartLine` is the base class line
    /// count plus two. The closest vm frame at or after that line is the
    /// model frame that called park (directly or through where). The marker
    /// is `>| ` on the suspended line and ` | ` everywhere else; a missing
    /// stack degrades to ` | ` on every line, never to a failed park.
    let sourceWithExecutionMarker (baseClassSource: string) (modelSource: string) (stack: string) : string =
        let lines = modelSource.Split('\n')
        // Wrapper lines: 1 = "(async () => {", then the base class, then the
        // model source.
        let modelStartLine = baseClassSource.Split('\n').Length + 2
        let modelEndLine = modelStartLine + lines.Length - 1
        let currentLine = locateSuspendedLine modelStartLine modelEndLine stack

        lines
        |> Array.mapi (fun index line -> (if index = currentLine then ">| " else " | ") + line)
        |> String.concat "\n"
        |> fun marked -> marked + "\n"

    /// Install the park signal the workflow races against the program promise.
    /// The returned Task completes with the parked payload when the program's
    /// `park(payload)` member fires; the resolver is stored in
    /// `parkState.ParkSignalResolve` so the park member can call it.
    let installParkSignal (parkState: ParkState) : Task<obj> =
        parkState.ParkRequested <- false
        parkState.ParkPayload <- null
        parkState.ParkSignalResolve <- null

        let signal: obj =
            emitJsExpr
                (parkState)
                "(() => { const state = $0; return new Promise((resolve) => { state.ParkSignalResolve = resolve; }); })()"

        signal :?> Task<obj>

    /// Suspend the program through the shared park channel: mark the request,
    /// fire the signal the workflow races against, and hand back the deferred
    /// promise the program awaits until unpark resolves it (or abandon
    /// rejects it). Callers own the abandon-window refusal and the stack
    /// capture so this stays the one suspension mechanism.
    let suspendProgram (parkState: ParkState) (payload: obj) : obj =
        parkState.ParkRequested <- true
        parkState.ParkPayload <- payload

        if not (isNull parkState.ParkSignalResolve) then
            emitJsExpr (parkState.ParkSignalResolve, payload) "$0($1)" |> ignore

        emitJsExpr
            parkState
            "(function(state) { return new Promise(function(resolve, reject) { state.ParkDeferredResolve = resolve; state.ParkDeferredReject = reject; }); })($0)"

    /// Resolve the deferred promise the program is awaiting at `await this.park`.
    /// Called by `unpark` with the injected value; the program continues as if
    /// the await returned that value.
    let unpark (parkState: ParkState) (value: obj) : unit =
        if not (isNull parkState.ParkDeferredResolve) then
            emitJsExpr (parkState.ParkDeferredResolve, value) "$0($1)" |> ignore

    /// Reject the deferred promise with the `__jsAbandoned` marker. Called by
    /// `abandon`/`reap`; the rejection unwinds the program stack from the await
    /// point so native try/finally blocks run as a finalizer.
    let abandon (parkState: ParkState) : unit =
        parkState.AbandonRequested <- true

        if not (isNull parkState.ParkDeferredReject) then
            emitJsExpr
                (parkState.ParkDeferredReject, abandonedSentinel)
                "$0(Object.assign(new Error($1), { __jsAbandoned: true, message: $1 }))"
            |> ignore
