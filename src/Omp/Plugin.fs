module Wanxiangshu.Omp.Plugin

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Omp.PluginCore

let private registered: obj = emitJsExpr () "new WeakSet()"

[<ExportDefault>]
let wanxiangshuExtension (pi: obj) : JS.Promise<unit> =
    promise {
        if registered?has(pi) then
            ()
        else
            registered?add(pi) |> ignore
            do! pluginFor pi
    }
