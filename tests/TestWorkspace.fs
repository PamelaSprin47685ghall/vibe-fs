module Wanxiangshu.Tests.TestWorkspace

open Fable.Core
open Fable.Core.JsInterop

module Dyn = Wanxiangshu.Runtime.Dyn

[<Import("createRequire", "node:module")>]
let private createRequire': string -> (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta: obj = jsNative

let private requireFn: string -> obj = createRequire' (string importMeta?url)

let private fsAsync: obj = Dyn.get (requireFn "fs") "promises"
let private pathModule: obj = requireFn "path"
let private osModule: obj = requireFn "os"

let mkdtempAsync (prefix: string) : JS.Promise<string> =
    unbox (fsAsync?mkdtemp (pathModule?join (osModule?tmpdir (), prefix)))

let private rmOne (path: string) : JS.Promise<unit> =
    unbox<JS.Promise<unit>> (fsAsync?rm (path, box {| recursive = true; force = true |}))

let rec private rmRetry (path: string) (attempt: int) : JS.Promise<unit> =
    promise {
        try
            do! rmOne path
        with ex ->
            let code = Dyn.str (box ex) "code"

            if (code = "ENOTEMPTY" || code = "EBUSY" || code = "EPERM") && attempt < 10 then
                do! Promise.sleep 100
                return! rmRetry path (attempt + 1)
            else
                return raise ex
    }

let rmAsync (path: string) : JS.Promise<unit> = rmRetry path 0

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
