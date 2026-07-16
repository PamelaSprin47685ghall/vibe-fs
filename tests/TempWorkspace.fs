module Wanxiangshu.Tests.TempWorkspace

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn

[<Import("createRequire", "node:module")>]
let private createRequire': string -> (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta: obj = jsNative

let private requireFn: string -> obj = createRequire' (string importMeta?url)

let private fsAsync: obj = get (requireFn "fs") "promises"
let private pathModule: obj = requireFn "path"
let private osModule: obj = requireFn "os"

let mkdtempAsync (prefix: string) : JS.Promise<string> =
    unbox (fsAsync?mkdtemp (pathModule?join (osModule?tmpdir (), prefix)))

let rmAsync (path: string) : JS.Promise<unit> =
    unbox (fsAsync?rm (path, box {| recursive = true; force = true |}))

let writeFileAsync (path: string) (content: string) : JS.Promise<unit> =
    unbox (fsAsync?writeFile (path, content))

let tryReadFileAsync (path: string) : JS.Promise<string option> =
    promise {
        try
            let! content = unbox<JS.Promise<string>> (fsAsync?readFile (path, box "utf8"))
            return Some content
        with _ ->
            return None
    }
