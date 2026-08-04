namespace Wanxiangshu.Infrastructure.Resources

open System
open Fable.Core
open Fable.Core.JsInterop

/// Fixed package-relative resource reads.
/// Compiled module lives at dist/Infrastructure/Resources/; package root is ../../../.
module PackageResources =

    [<Import("readFileSync", "node:fs")>]
    let private readFileSync (path: string, encoding: string) : string = jsNative

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    [<Import("fileURLToPath", "node:url")>]
    let private fileURLToPath (url: string) : string = jsNative

    [<Import("dirname", "node:path")>]
    let private dirname (path: string) : string = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string, b: string) : string = jsNative

    [<Emit("import.meta.url")>]
    let private importMetaUrl: string = jsNative

    let private packageRoot () =
        let here = dirname (fileURLToPath importMetaUrl)
        pathJoin (pathJoin (pathJoin (here, ".."), ".."), "..")

    /// Read `resources/<relativeResourcePath>` under the package root.
    /// No cwd walk, no candidate search, no dist/src fallback.
    let readText (relativeResourcePath: string) : string =
        let full = pathJoin (pathJoin (packageRoot (), "resources"), relativeResourcePath)

        if not (existsSync full) then
            raise (InvalidOperationException(sprintf "package resource missing: %s" full))

        readFileSync (full, "utf8")
