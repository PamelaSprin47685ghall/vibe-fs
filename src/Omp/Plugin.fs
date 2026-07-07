module Wanxiangshu.Omp.Plugin

open Fable.Core
open Wanxiangshu.Omp.PluginCore

let private registered = JS.Constructors.Set.Create<obj>()

[<ExportDefault>]
let wanxiangshuExtension (pi: obj) : JS.Promise<unit> =
    promise {
        if registered.has(pi) then
            ()
        else
            registered.add(pi) |> ignore
            do! pluginFor pi
    }
