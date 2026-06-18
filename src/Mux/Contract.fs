module VibeFs.Mux.Contract

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel

type JsonSchema =
    { ``type``: string
      properties: obj
      required: string array option
      additionalProperties: bool option }

/// A tool's declarative contract plus its executor. Pure data except `execute`.
type ToolDefinition =
    { name: string
      description: string
      parameters: JsonSchema
      execute: obj -> obj -> JS.Promise<string>
      condition: (obj -> bool) option }

let mutable registeredToolNames: string array = [||]

let resolveStr (s: string) : JS.Promise<string> = async { return s } |> Async.StartAsPromise

let jsonStringify (o: obj) : string = JS.JSON.stringify(o)

let optInt (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some(unbox<int> v)
let optBool (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some(unbox<bool> v)
let optField (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some v

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

/// A structural view of a host tool, for wrapping without the ai-sdk dependency.
let asToolLike (tool: obj) : obj = tool

/// Require a workspaceId from the config, returning an error Result when absent.
let requireWorkspaceId (config: obj) (toolName: string) : Result<string, string> =
    let wid = config?("workspaceId")
    if isNull wid || string wid = "" then Error $"{toolName} requires workspaceId"
    else Ok(string wid)
