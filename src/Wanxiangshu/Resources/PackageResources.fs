namespace Wanxiangshu.Resources
open Wanxiangshu.Change
open Wanxiangshu.Enforcer
open Wanxiangshu.Git
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength.Persistence

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

    [<Import("readdirSync", "node:fs")>]
    let private readdirSync (path: string) : string array = jsNative

    [<Import("statSync", "node:fs")>]
    let private statSync (path: string) : obj = jsNative

    [<Import("fileURLToPath", "node:url")>]
    let private fileURLToPath (url: string) : string = jsNative

    [<Import("dirname", "node:path")>]
    let private dirname (path: string) : string = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string, b: string) : string = jsNative

    [<Emit("import.meta.url")>]
    let private importMetaUrl: string = jsNative

    [<Emit("$0.isDirectory()")>]
    let private statsIsDirectory (stats: obj) : bool = jsNative

    let private packageRoot () =
        let here = dirname (fileURLToPath importMetaUrl)
        pathJoin (pathJoin (pathJoin (here, ".."), ".."), "..")

    let private resourcesRoot () = pathJoin (packageRoot (), "resources")

    let private resolveResource (relativeResourcePath: string) : string =
        pathJoin (resourcesRoot (), relativeResourcePath)

    /// Read `resources/<relativeResourcePath>` under the package root.
    /// No cwd walk, no candidate search, no dist/src fallback.
    let readText (relativeResourcePath: string) : string =
        let full = resolveResource relativeResourcePath

        if not (existsSync full) then
            raise (InvalidOperationException(sprintf "package resource missing: %s" full))

        readFileSync (full, "utf8")

    /// True when `resources/<relativeResourcePath>` exists (file or directory).
    let exists (relativeResourcePath: string) : bool =
        existsSync (resolveResource relativeResourcePath)

    /// Child directory basenames under `resources/<relativeDir>`, lexical sort.
    /// Files (e.g. leftover catalog.json) are ignored. Missing parent → throw.
    let listChildDirectoryNames (relativeDir: string) : string list =
        let full = resolveResource relativeDir

        if not (existsSync full) then
            raise (InvalidOperationException(sprintf "package resource missing: %s" full))

        readdirSync full
        |> Array.toList
        |> List.filter (fun name ->
            try
                statsIsDirectory (statSync (pathJoin (full, name)))
            with _ ->
                false)
        |> List.sort
