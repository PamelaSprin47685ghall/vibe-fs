module VibeFs.Kernel.RecordValidator

open Fable.Core
open Fable.Core.JsInterop

/// A parser turns an unknown JS value into a typed Result.
type Parser<'t> = obj -> Result<'t, string>

/// Parse a branded string id: non-empty strings pass, everything else fails.
let parseId (label: string) : Parser<string> =
    fun input ->
        let s = string input
        if System.String.IsNullOrEmpty s then Error $"{label} must be a non-empty string"
        else Ok s

/// Build a plain JS object from key/value pairs (NOT a Fable map/tuple array),
/// so callers can read fields with `result.path` exactly like the original TS.
[<Emit("(() => { const o = {}; for (const kv of $0) o[kv[0]] = kv[1]; return o; })()")>]
let private objFromPairs (pairs: (string * obj) array) : obj = jsNative

/// Build a plain JS object of field-name → error-message for the failure case.
[<Emit("(() => { const o = {}; for (const kv of $0) o[kv[0]] = kv[1]; return o; })()")>]
let private errorsFromPairs (pairs: (string * string) array) : obj = jsNative

/// Read a property by string key from a dynamic object.
[<Emit("$0[$1]")>]
let private getKey (o: obj) (key: string) : obj = jsNative

/// Validate a record of named fields against a schema of parsers.  Independent
/// fields are all checked — the caller sees every failure at once.  Returns a
/// plain JS object on success (readable as `result.path`) or an error object.
let validateRecord (schema: (string * Parser<obj>) list) (input: obj)
    : Result<obj, obj> =
    let parsed = ResizeArray<string * obj>()
    let errors = ResizeArray<string * string>()
    for (key, parse) in schema do
        let fieldValue = getKey input key
        match parse fieldValue with
        | Ok value -> parsed.Add(key, value)
        | Error message -> errors.Add(key, message)
    if errors.Count > 0 then Error(errorsFromPairs (errors.ToArray()))
    else Ok(objFromPairs (parsed.ToArray()))
