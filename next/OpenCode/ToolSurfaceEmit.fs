namespace Wanxiangshu.Next.OpenCode

#nowarn "3511"

open System
open Fable.Core
open Fable.Core.JsInterop

/// Shared JS interop primitives for the tool surface modules.
module ToolSurfaceEmit =

    [<Emit("$0.schema.string()")>]
    let stringSchema (tool: obj) : obj = jsNative

    [<Emit("$0.schema.enum($1)")>]
    let enumSchema (tool: obj) (values: string array) : obj = jsNative

    [<Emit("$0.schema.enum($1).optional()")>]
    let optionalEnumSchema (tool: obj) (values: string array) : obj = jsNative

    [<Emit("$0.schema.string().optional()")>]
    let optionalStringSchema (tool: obj) : obj = jsNative

    [<Emit("$0($1)")>]
    let applyTool (factory: obj) (definition: obj) : obj = jsNative

    [<Emit("(args, context) => $0(args)(context)")>]
    let uncurriedExecute (fn: obj) : obj = jsNative

    [<Emit("Math.random().toString(36).slice(2, 8)")>]
    let newAgentId () : string = jsNative

    [<Emit("JSON.stringify($0)")>]
    let stringify (value: obj) : string = jsNative

    let contextString (ctx: obj) (name: string) =
        if isNull ctx || isNull ctx?(name) then
            None
        else
            let v = unbox<string> ctx?(name) in if String.IsNullOrWhiteSpace v then None else Some v

    let textArg (args: obj) (name: string) =
        if isNull args || isNull args?(name) then
            ""
        else
            unbox<string> args?(name)

    let optionalTextArg (args: obj) (name: string) =
        let value = textArg args name
        if String.IsNullOrWhiteSpace value then None else Some value
