// primary_owner: distribution — Distribution.SurfaceSurface — KEEP — distribution-surface verified
namespace Wanxiangshu.Resources

open System
open Fable.Core
open Fable.Core.JsInterop

/// Package manifest metadata reads, fixed to the installed package root.
/// Compiled module lives at dist/Resources/; package root is ../...
/// No cwd walk, no candidate search, no fallback.
module PackageMetadata =

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

    [<Emit("JSON.parse($0)")>]
    let private parseJson (text: string) : obj = jsNative

    /// dist/<Area>/<Module>.js is always two levels below the package root.
    let packageRoot () =
        let here = dirname (fileURLToPath importMetaUrl)
        pathJoin (pathJoin (here, ".."), "..")

    /// Published package version from the manifest at the package root.
    let version () : string =
        let manifest = pathJoin (packageRoot (), "package.json")

        if not (existsSync manifest) then
            raise (InvalidOperationException(sprintf "package manifest missing: %s" manifest))

        let parsed = parseJson (readFileSync (manifest, "utf8"))
        let version: string = parsed?version

        if String.IsNullOrWhiteSpace version then
            raise (InvalidOperationException(sprintf "package manifest version missing: %s" manifest))

        version
