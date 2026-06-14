module VibeFs.Opencode.Sdk

open Fable.Core
open VibeFs.Kernel

/// The opencode plugin SDK's `tool` factory + `tool.schema` (Zod-like) builder.
[<Import("tool", "@opencode-ai/plugin/tool")>]
let toolFactory : obj = jsNative
let schema : obj = Dyn.get toolFactory "schema"

/// `o.method()` and `o.method(arg)` — explicit object-first, no pipelines.
[<Emit("$0[$1]()")>]
let call0 (o: obj) (method: string) : obj = jsNative
[<Emit("$0[$1]($2)")>]
let call1 (o: obj) (method: string) (arg: obj) : obj = jsNative

let private str () = call0 schema "string"
let arr (item: obj) : obj = call1 schema "array" item
let private tuple (items: obj array) : obj = call1 schema "tuple" items
let private union' (items: obj array) : obj = call1 schema "union" items

let strMin (minLen: int) (desc: string) : obj =
    call1 (call1 (str ()) "min" (box minLen)) "describe" (box desc)
let strMinNullish (minLen: int) (desc: string) : obj =
    call1 (call0 (call1 (str ()) "min" (box minLen)) "nullish") "describe" (box desc)
let strReq (desc: string) : obj = call1 (str ()) "describe" (box desc)
let strOpt (desc: string) : obj = call1 (call0 (str ()) "nullish") "describe" (box desc)
let intMinNullish (minVal: int) (desc: string) : obj =
    let n = call1 (call0 schema "number") "int" (box 0)
    let n = call1 n "min" (box minVal)
    call1 (call0 n "nullish") "describe" (box desc)
let boolOpt (desc: string) : obj = call1 (call0 (call0 schema "boolean") "nullish") "describe" (box desc)
let excludeOpt (desc: string) : obj =
    let s = str ()
    call1 (call0 (union' [| s; arr s |]) "nullish") "describe" (box desc)
let intentsSchema (desc: string) : obj =
    let inner = tuple [| strMin 1 ""; call1 (arr (strMin 1 "")) "min" (box 1) |]
    call1 (call1 (arr inner) "min" (box 1)) "describe" (box desc)
let uiParam : obj = call1 (call0 (str ()) "optional") "describe" (box "Internal: populated by hook")
let strArrayOpt (desc: string) : obj = call1 (call0 (arr (strMin 1 "")) "optional") "describe" (box desc)
let numOpt (desc: string) : obj =
    let n = call0 schema "number"
    let n = call0 n "int"
    let n = call0 n "positive"
    call1 (call0 n "optional") "describe" (box desc)
let enumOpt (values: string array) (desc: string) : obj =
    call1 (call0 (call1 schema "enum" (box values)) "optional") "describe" (box desc)

[<Emit("$0($1)")>]
let private invokeTool (factory: obj) (config: obj) : obj = jsNative
let define (description: string) (args: obj) (execute: obj -> obj -> JS.Promise<string>) : obj =
    invokeTool toolFactory (box {| description = description; args = args; execute = execute |})
