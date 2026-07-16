module Wanxiangshu.Hosts.Omp.NudgeToolFilter

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.OmpSessionTools

module Dyn = Wanxiangshu.Runtime.Dyn

let applyActiveToolFilterForMainSession (piObj: obj) (ctxObj: obj) : JS.Promise<unit> =
    promise {
        let active =
            if Dyn.typeIs (Dyn.get piObj "getActiveTools") "function" then
                unbox<obj array> (piObj?getActiveTools ()) |> Array.map string
            else
                [||]

        let filtered = filterOmpMainSessionActiveTools active

        if filtered.Length <> active.Length then
            if Dyn.typeIs (Dyn.get piObj "setActiveTools") "function" then
                do! piObj?setActiveTools (filtered)
    }
