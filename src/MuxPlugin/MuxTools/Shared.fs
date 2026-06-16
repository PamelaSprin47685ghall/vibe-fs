module VibeFs.MuxPlugin.MuxTools.Shared

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Mux.Contract

/// Populated by `MuxTools.createToolCatalog` at registration time so that
/// `experimentsFor` can compute the disabled-tool list via `canUse`.
let mutable registeredToolNames: string array = [||]

let resolveStr (s: string) : JS.Promise<string> = async { return s } |> Async.StartAsPromise

let jsonStringify (o: obj) : string = JS.JSON.stringify(o)

let optInt (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some(unbox<int> v)
let optBool (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some(unbox<bool> v)
let optField (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some v

/// Read a string field; returns None when absent (null/undefined), Some "" for empty string.
let strField (a: obj) (k: string) : string option =
    let v = Dyn.get a k
    if Dyn.isNullish v then None else Some(string v)

let requireStrArray (a: obj) (k: string) : string array =
    let v = Dyn.get a k
    if Dyn.isNullish v || not (Dyn.isArray v) then [||]
    else v :?> obj array |> Array.map string

let mkSchema (props: obj) (required: string array) : JsonSchema =
    { ``type`` = "object"; properties = props; required = Some required; additionalProperties = Some false }

let strProp (desc: string) : obj = createObj [ "type", box "string"; "description", box desc ]
let numProp (desc: string) : obj = createObj [ "type", box "number"; "description", box desc ]
let boolProp (desc: string) : obj = createObj [ "type", box "boolean"; "description", box desc ]
let strEnumProp (desc: string) (values: string array) : obj = createObj [ "type", box "string"; "enum", box values; "description", box desc ]
let strArrayProp (desc: string) : obj =
    createObj [ "type", box "array"; "items", box (createObj [ "type", box "string" ]); "description", box desc ]
