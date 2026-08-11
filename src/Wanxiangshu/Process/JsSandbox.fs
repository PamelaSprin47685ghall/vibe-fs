namespace Wanxiangshu.Process

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain

/// JS-011/JS-053 sandbox: arbitrary model JavaScript runs in a Node vm
/// context that carries only the injected `api` object — no fs / process /
/// network / env globals reach the program. `new Function` is just the
/// invocation mechanism; the vm context is the authority boundary.
module JsSandbox =

    [<Import("createContext", "node:vm")>]
    let private createContext (sandbox: obj) : obj = jsNative

    [<Import("runInContext", "node:vm")>]
    let private runInContext (code: string) (context: obj) (options: obj) : obj = jsNative

    [<Emit("JSON.parse($0)")>]
    let private parseJson (text: string) : obj = jsNative

    /// Sentinel payloads the wrapper returns instead of the program result;
    /// recognized by prefix, never by exception-message sniffing (JS-019).
    let private failedPrefix = "{\"__jsProgramFailed\":"
    let private hostFailedPrefix = "{\"__jsHostFailed\":"
    let private invalidReturnPrefix = "{\"__jsInvalidReturn\":"

    /// Wrap base class + model source into an async IIFE that evaluates to a
    /// JSON string. The api is proxied so every member call re-checks the
    /// injected absolute deadline: synchronous segments are bounded by the vm
    /// timeout, async segments by the proxy (JS-054.1).
    let wrapProgram (baseClassSource: string) (modelSource: string) (deadlineEpochMs: int64) : string =
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
        + "        if (Date.now() > "
        + string deadlineEpochMs
        + ") {\n"
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
        + "  return JSON.stringify({ __jsProgramFailed: true, message: String((__e && __e.message) || __e) });\n"
        + "}\n"
        + "try { return JSON.stringify(__result); }\n"
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

    /// Execute the wrapped program in a fresh vm context.
    ///
    /// `deadlineMs` bounds the synchronous segment via the vm timeout; the
    /// injected proxy bounds async segments. `outputBoundBytes` bounds the
    /// serialized result (JS-054.2).
    let run
        (wrappedSource: string)
        (api: obj)
        (deadlineMs: int)
        (outputBoundBytes: int)
        : Task<Result<string, JsFailure>> =
        task {
            try
                let context = createContext (createObj [ "api" ==> api ])

                let promise =
                    runInContext wrappedSource context (createObj [ "timeout" ==> deadlineMs ])

                let! json = promise :?> Task<string>

                if json.StartsWith hostFailedPrefix then
                    return Error(decodeHostFailed json)
                elif json.StartsWith failedPrefix then
                    return Error(decodeProgramFailed json)
                elif json.StartsWith invalidReturnPrefix then
                    return Error JsFailure.InvalidReturnValue
                elif json.Length > outputBoundBytes then
                    return Error(JsFailure.ResultTooLarge None)
                else
                    return Ok json
            with ex ->
                return Error(classifySyncError ex)
        }

    /// Run a program with the surface the generator produced: base class
    /// source, model source, and a binding object. Convenience entry that
    /// keeps call sites free of wrapper details (JS-002/011).
    let runSurface
        (baseClassSource: string)
        (modelSource: string)
        (api: obj)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (outputBoundBytes: int)
        : Task<Result<string, JsFailure>> =
        run (wrapProgram baseClassSource modelSource deadlineEpochMs) api deadlineMs outputBoundBytes
