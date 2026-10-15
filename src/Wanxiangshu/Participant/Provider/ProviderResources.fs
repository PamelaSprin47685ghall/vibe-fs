namespace Wanxiangshu.Participant.Provider

open System
open Fable.Core
open Fable.Core.JsInterop

/// Bilingual provider assets: `resources/provider/<semantic>/<en.md|zh-CN.md>`
/// (PROMPT-017 §4.7.8). Pairing, package-anchored location and reads are
/// owned by `wanxiangli/resources`; this module binds that owner to this
/// package's root and keeps the host-side typed surface.
[<RequireQualifiedAccess>]
module ProviderResources =

    [<Import("Resources", "wanxiangli/resources")>]
    let private library: obj = jsNative

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string, b: string) : string = jsNative

    [<Import("dirname", "node:path")>]
    let private dirname (path: string) : string = jsNative

    [<Import("fileURLToPath", "node:url")>]
    let private fileURLToPath (url: string) : string = jsNative

    [<Emit("import.meta.url")>]
    let private importMetaUrl: string = jsNative

    /// dist/<Area>/<Module>.js walks up to the nearest package.json.
    /// No cwd, no candidate search, no source-tree fallback.
    let private packageRoot () : string =
        let rec walk (dir: string) : string =
            if isNull dir then
                raise (InvalidOperationException "package root not found")
            elif existsSync (pathJoin (dir, "package.json")) then
                dir
            else
                walk (dirname dir)

        walk (dirname (fileURLToPath importMetaUrl))

    // DSL-MUTABLE: resource — lazily created library binding to this package root
    let mutable private instance: obj = null

    let private resources () : obj =
        if isNull instance then
            instance <- emitJsExpr (library, packageRoot ()) "new $0($1)"

        instance

    let relativePath (language: ProviderLanguage) (semanticPath: string) : string =
        resources()?relativePath(ProviderLanguage.externalName language, semanticPath)

    let exists (language: ProviderLanguage) (semanticPath: string) : bool =
        resources()?exists(ProviderLanguage.externalName language, semanticPath)

    let readText (language: ProviderLanguage) (semanticPath: string) : string =
        resources()?readText(ProviderLanguage.externalName language, semanticPath)

    /// ARCH-016 Gate C hook: both locale leaves must exist for a semantic path.
    let requireLanguagePair (semanticPath: string) : unit =
        resources()?requireLanguagePair semanticPath |> ignore

    /// Layout smoke: provider tree root present.
    let languageRootsPresent () : bool = resources()?resourceExists "provider"
