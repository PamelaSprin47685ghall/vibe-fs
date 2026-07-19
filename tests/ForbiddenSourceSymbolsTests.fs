module Wanxiangshu.Tests.ForbiddenSourceSymbolsTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureGatesFs

[<Emit("process.cwd()")>]
let private cwd () : string = jsNative

let run () : unit =
    let srcRoot = pathJoin (cwd ()) "src"

    let forbidden =
        [ "_satisfyArchTest"
          "_intentsRawFromArgsUsedInCore"
          "fallbackRuntimeInstance" ]

    let files = collectFsFiles srcRoot

    for file in files do
        let content = readFileSync file "utf-8"

        for symbol in forbidden do
            check (sprintf "forbidden symbol absent in %s: %s" file symbol) (not (content.Contains symbol))
