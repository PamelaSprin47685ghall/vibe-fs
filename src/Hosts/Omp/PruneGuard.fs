module Wanxiangshu.Hosts.Omp.PruneGuard

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Omp.PiResolve

[<Import("join", "node:path")>]
let private pathJoin (a: string) (b: string) : string = jsNative

[<Import("pathToFileURL", "node:url")>]
let private pathToFileURL (p: string) : obj = jsNative

let patchDisablePrune () : JS.Promise<unit> =
    promise {
        try
            let basePath = getPiBase ()

            let href =
                pathToFileURL (pathJoin basePath "pi-agent-core/src/compaction/pruning.ts")?href

            let! pruning = importDynamic<obj> (string href)
            let config = Dyn.get pruning "DEFAULT_PRUNE_CONFIG"

            if Dyn.isNullish config then
                ()
            else
                for key in [| "protectTokens"; "minimumSavings" |] do
                    try
                        config?(key) <- System.Double.MaxValue
                    with _ ->
                        try
                            emitJsExpr
                                (config, key, System.Double.MaxValue)
                                "Object.defineProperty($0, $1, { value: $2, configurable: true, writable: true })"
                            |> ignore
                        with _ ->
                            ()
        with _ ->
            ()
    }
