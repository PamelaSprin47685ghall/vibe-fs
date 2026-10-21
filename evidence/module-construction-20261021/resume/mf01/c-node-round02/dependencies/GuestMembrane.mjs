// .build/guest-membrane-53132/Sandbox.js
import { runInContext, createContext } from "node:vm";
var installSource = `
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
  var Number = globalThis.Number;
  var isSafeInteger = Number.isSafeInteger;
  var JSON = globalThis.JSON;
  var isArray = Array.isArray;
  var getOwnNames = Object.getOwnPropertyNames;
  var defineProperty = Object.defineProperty;
  function own(target, key, value) {
    defineProperty(target, key, { value: value, enumerable: true, writable: true, configurable: true });
  }
  // Capture intrinsic methods before any model code runs, and invoke them via
  // the captured Reflect.apply. The membrane shares its realm with the model
  // program, so a model that monkeypatches e.g.
  // WeakMap.prototype.has would otherwise see the raw host value the membrane
  // passes to that method. Holding direct references defeats later tampering.
  var ReflectApply = Reflect.apply;
  var ReflectGet = Reflect.get;
  var ReflectConstruct = Reflect.construct;
  var WeakMapHas = WeakMap.prototype.has;
  var WeakMapGet = WeakMap.prototype.get;
  var WeakMapSet = WeakMap.prototype.set;
  function mapHas(m, k) { return ReflectApply(WeakMapHas, m, [k]); }
  function mapGet(m, k) { return ReflectApply(WeakMapGet, m, [k]); }
  function mapSet(m, k, v) { ReflectApply(WeakMapSet, m, [k, v]); }
  function argsToArray(a) { var out = []; for (var i = 0; i < a.length; i++) own(out, i, a[i]); return out; }
  function mapArray(arr, fn) { var out = []; for (var i = 0; i < arr.length; i++) own(out, i, fn(arr[i])); return out; }
  var slot = null;
  var invocation = null;
  globalThis.__put = function (v) { slot = v; };
  globalThis.__take = function () { var s = slot; slot = null; return s; };
  globalThis.__putInvocation = function (value) { invocation = value; };
  defineProperty(globalThis, "__runInvocation", {
    value: function () {
      var next = invocation;
      invocation = null;
      if (next === null) return undefined;
      return next();
    },
    enumerable: false,
    configurable: false,
    writable: false
  });
  globalThis.__makeRunner = function (hostApi, baseClassSource, modelSource, hostValues) {
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
        var minimum = hostErr.__jsFailure.minimumOutputBudgetBytes;
        if (isSafeInteger(minimum) && minimum > 0) g.__jsFailure.minimumOutputBudgetBytes = minimum;
      }
      if (hostErr && hostErr.__jsAbandoned) g.__jsAbandoned = true;
      if (hostErr && hostErr.__jsDelivered) g.__jsDelivered = wrap(hostErr.__jsDelivered);
      if (hostErr && hostErr.code === "ERR_SCRIPT_EXECUTION_TIMEOUT") {
        g.__jsFailure = { code: "PROGRAM_TIMEOUT", reason: "program exceeded its deadline" };
      }
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
      if (mapHas(guestToHost, guestInst)) return mapGet(guestToHost, guestInst);
      var proxy = new Proxy(guestInst, {
        get: function (target, prop) {
          if (prop === "constructor") return undefined;
          var v = hostValues.invoke(function () { return ReflectGet(target, prop, target); });
          if (typeof v !== "function") {
            if (isPrimitive(v)) return v;
            return hostValues.invoke(function () { return unwrap(v); });
          }
          return function () {
            var hostArgs = argsToArray(arguments);
            var guestArgs = mapArray(hostArgs, wrap);
            return hostValues.invoke(function () {
              try {
                return unwrap(ReflectApply(v, target, guestArgs));
              } catch (e) {
                throw unwrap(e);
              }
            });
          };
        },
        set: function () { return false; },
        defineProperty: function () { return false; },
        deleteProperty: function () { return false; },
        getPrototypeOf: function () { return null; },
      });
      mapSet(guestToHost, guestInst, proxy);
      mapSet(hostToGuest, proxy, guestInst);
      return proxy;
    }
    function wrap(hostValue) {
      if (isPrimitive(hostValue)) return hostValue;
      if (mapHas(hostToGuest, hostValue)) return mapGet(hostToGuest, hostValue);
      if (looksLikeError(hostValue)) {
        var ge = copyError(hostValue);
        mapSet(hostToGuest, hostValue, ge);
        mapSet(guestToHost, ge, hostValue);
        return ge;
      }
      if (typeof hostValue.then === "function") {
        var gp = new Promise(function (resolve, reject) {
          hostValue.then(
            function (v) { resolve(wrap(v)); },
            function (e) { reject(wrap(e)); }
          );
        });
        mapSet(hostToGuest, hostValue, gp);
        mapSet(guestToHost, gp, hostValue);
        return gp;
      }
      if (typeof hostValue === "function") {
        var guestFn = function () {
          var guestArgs = argsToArray(arguments);
          var hostArgs = mapArray(guestArgs, unwrap);
          try {
            return wrap(ReflectApply(hostValue, unwrap(this), hostArgs));
          } catch (e) {
            throw wrap(e);
          }
        };
        mapSet(hostToGuest, hostValue, guestFn);
        mapSet(guestToHost, guestFn, hostValue);
        return guestFn;
      }
      if (isArray(hostValue)) {
        var ga = [];
        mapSet(hostToGuest, hostValue, ga);
        mapSet(guestToHost, ga, hostValue);
        for (var i = 0; i < hostValue.length; i++) own(ga, i, wrap(hostValue[i]));
        return ga;
      }
      var go = {};
      mapSet(hostToGuest, hostValue, go);
      mapSet(guestToHost, go, hostValue);
      var names = getOwnNames(hostValue);
      for (var j = 0; j < names.length; j++) {
        var k = names[j];
        try { own(go, k, wrap(hostValue[k])); } catch (_e) {}
      }
      return go;
    }
    function unwrap(guestValue) {
      if (isPrimitive(guestValue)) return guestValue;
      if (mapHas(guestToHost, guestValue)) return mapGet(guestToHost, guestValue);
      if (typeof guestValue === "function") {
        var hostFn = function () {
          var hostArgs = argsToArray(arguments);
          var guestArgs = mapArray(hostArgs, wrap);
          var constructing = new.target !== undefined;
          var receiver = this;
          return hostValues.invoke(function () {
            try {
              if (constructing) {
                var inst = ReflectConstruct(guestValue, guestArgs, guestValue);
                return bridgeInstance(inst);
              }
              return unwrap(ReflectApply(guestValue, wrap(receiver), guestArgs));
            } catch (e) {
              throw unwrap(e);
            }
          });
        };
        mapSet(guestToHost, guestValue, hostFn);
        mapSet(hostToGuest, hostFn, guestValue);
        return hostFn;
      }
      if (typeof guestValue.then === "function") {
        var hp = hostValues.promise(function (resolve, reject) {
          guestValue.then(
            function (v) { resolve(unwrap(v)); },
            function (e) { reject(unwrap(e)); }
          );
        });
        mapSet(guestToHost, guestValue, hp);
        mapSet(hostToGuest, hp, guestValue);
        return hp;
      }
      if (isArray(guestValue)) {
        var ha = hostValues.array();
        mapSet(guestToHost, guestValue, ha);
        mapSet(hostToGuest, ha, guestValue);
        for (var i2 = 0; i2 < guestValue.length; i2++) own(ha, i2, unwrap(guestValue[i2]));
        return ha;
      }
      var ho = hostValues.record();
      mapSet(guestToHost, guestValue, ho);
      mapSet(hostToGuest, ho, guestValue);
      var gnames = getOwnNames(guestValue);
      for (var j2 = 0; j2 < gnames.length; j2++) {
        var gk = gnames[j2];
        try { own(ho, gk, unwrap(guestValue[gk])); } catch (_e2) {}
      }
      return ho;
    }
    var api = wrap(hostApi);
    var body =
      "(async () => {\\n" +
      baseClassSource +
      "\\n" +
      modelSource +
      "\\n" +
      "let __result;\\n" +
      "try { __result = await new Js(api).run(); }\\n" +
      "catch (__e) {\\n" +
      "  if (__e && __e.__jsFailure && typeof __e.__jsFailure.code === 'string') {\\n" +
      "    return JSON.stringify({\\n" +
      "      __jsHostFailed: true,\\n" +
      "      code: __e.__jsFailure.code,\\n" +
      "      reason: String(__e.__jsFailure.reason || __e.__jsFailure.code),\\n" +
      "      minimumOutputBudgetBytes: __e.__jsFailure.minimumOutputBudgetBytes\\n" +
      "    });\\n" +
      "  }\\n" +
      "  if (__e && __e.__jsAbandoned) {\\n" +
      "    return JSON.stringify({ __jsAbandoned: true });\\n" +
      "  }\\n" +
      "  if (__e && __e.__jsDelivered) {\\n" +
      "    return JSON.stringify({ __jsDelivered: true, view: __e.__jsDelivered.view });\\n" +
      "  }\\n" +
      "  return JSON.stringify({ __jsProgramFailed: true, message: String((__e && __e.message) || __e) });\\n" +
      "}\\n" +
      "try {\\n" +
      "  const __json = JSON.stringify(__result);\\n" +
      "  return typeof __json === 'string' ? __json : JSON.stringify({ __jsInvalidReturn: true });\\n" +
      "}\\n" +
      "catch (__e2) { return JSON.stringify({ __jsInvalidReturn: true }); }\\n" +
      "})()";
    var runBody = new Function("api", "JSON", "return (" + body + ");");
    return runBody(api, JSON);
  };
})();
`;
function launch(api, baseClassSource, modelSource) {
  return (function(createContext2, runInContext2, INSTALL, api2, baseClass, model) {
    var ctx = createContext2(/* @__PURE__ */ Object.create(null), { microtaskMode: "afterEvaluate" });
    runInContext2(INSTALL, ctx, { timeout: 5e3 });
    var paused = false;
    var resumeWaiter = null;
    function pause() {
      paused = true;
    }
    function resume() {
      paused = false;
      if (resumeWaiter !== null) {
        var wake = resumeWaiter;
        resumeWaiter = null;
        wake();
      }
    }
    var put = runInContext2("__put", ctx);
    var putInvocation = runInContext2("__putInvocation", ctx);
    runInContext2("delete globalThis.__putInvocation", ctx);
    function invoke(action) {
      putInvocation(action);
      return runInContext2("__runInvocation()", ctx);
    }
    var hostValues = Object.freeze({
      record: function() {
        return {};
      },
      array: function() {
        return [];
      },
      promise: function(executor) {
        return new Promise(executor);
      },
      invoke
    });
    put({ api: api2, base: baseClass, model, values: hostValues });
    var progPromise;
    try {
      progPromise = runInContext2(
        "(function(){ var t = __take(); var make = __makeRunner; delete globalThis.__put; delete globalThis.__take; delete globalThis.__makeRunner; return make(t.api, t.base, t.model, t.values); })()",
        ctx
      );
    } catch (e) {
      return { programPromise: Promise.reject(e), pause, resume };
    }
    var box = { done: false, value: null, error: null, failed: false };
    var register = runInContext2(
      "(function(p, box){ Promise.resolve(p).then(function(v){ box.done = true; box.value = v; }, function(e){ box.done = true; box.failed = true; box.error = e; }); })",
      ctx
    );
    register(progPromise, box);
    var driven = (async function() {
      while (!box.done) {
        if (paused) {
          await new Promise(function(resolve) {
            resumeWaiter = resolve;
          });
          continue;
        }
        runInContext2("void 0", ctx);
        if (box.done) break;
        await new Promise(function(r) {
          setImmediate(r);
        });
      }
      if (box.failed) throw box.error;
      return box.value;
    })();
    return { programPromise: driven, pause, resume };
  })(createContext, runInContext, installSource, api, baseClassSource, modelSource);
}
function Sandbox() {
  return {
    run(api, baseClassSource, modelSource) {
      return launch(api, baseClassSource, modelSource);
    }
  };
}
export {
  Sandbox
};
