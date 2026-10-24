namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

/// Deterministic gitignore-aware glob, bridged to `wanxiangli/runtime` (CUT-24).
/// The enumeration algorithm lives in the library; this module keeps the
/// main-repo signature stable.
module JsGlobFs =

    [<Import("Filesystem", "wanxiangli/runtime")>]
    let private Filesystem: obj = jsNative

    [<Emit("$0({ root: $1 })")>]
    let private createFilesystem (factory: obj) (root: string) : obj = jsNative

    [<Import("resolve", "node:path")>]
    let private pathResolve (path: string) : string = jsNative

    [<Import("relative", "node:path")>]
    let private pathRelative (from: string) (toPath: string) : string = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string) (b: string) : string = jsNative

    [<Import("isAbsolute", "node:path")>]
    let private pathIsAbsolute (path: string) : bool = jsNative

    type JsGlobListing = { Paths: string list }

    let private ofResult (result: obj) : Result<JsGlobListing, JsFailure> =
        if unbox<bool> (result?ok) then
            let value = result?value
            let paths = unbox<obj array> (value?paths) |> Array.map string |> Array.toList
            Ok { Paths = paths }
        else
            Error(JsFailure.ofWire (string (result?code)) (string (result?reason)))

    /// 单模式路径匹配（fsi 既有入口，无生产消费者；保留桥接以稳定签名）。
    let matchesPathPattern (pattern: string) (path: string) : Result<bool, JsFailure> =
        let rootlessApi = createFilesystem Filesystem ""
        let result = rootlessApi?matchesPathPattern (pattern, path)

        if unbox<bool> (result?ok) then
            Ok(unbox<bool> (result?value))
        else
            Error(JsFailure.ofWire (string (result?code)) (string (result?reason)))

    /// JS-007: gitignore-style glob. Full deterministic enumeration — no
    /// internal bound. An oversized result is tail-kept once, by the Host
    /// tool-result bound at the final boundary.
    let glob (root: string) (pattern: string) : Task<Result<JsGlobListing, JsFailure>> =
        let api = createFilesystem Filesystem root

        task {
            let! (result: obj) = unbox<Task<obj>> (api?glob (pattern))
            return ofResult result
        }

    type JsVisibleEntry = { Name: string; IsDir: bool }

    /// JS-007 / [057]: one-directory listing with gitignore semantics.
    /// Supports listing paths inside or outside the workspace root.
    let visibleEntries (root: string) (path: string) : Result<JsVisibleEntry list, JsFailure> =
        let full =
            if System.String.IsNullOrEmpty path then pathResolve root
            elif pathIsAbsolute path then pathResolve path
            else pathResolve (pathJoin root path)

        let rel = pathRelative root full

        let (dirRoot, dirPath) =
            if rel = "" || (not (rel.StartsWith "..") && not (pathIsAbsolute rel)) then
                (root, rel)
            else
                (full, "")

        let api = createFilesystem Filesystem dirRoot
        let result: obj = api?visibleEntries (dirPath)

        if unbox<bool> (result?ok) then
            let entries = unbox<obj array> (result?entries)

            entries
            |> Array.map (fun entry ->
                { Name = string (entry?name)
                  IsDir = unbox<bool> (entry?isDir) })
            |> Array.toList
            |> Ok
        else
            Error(JsFailure.ofWire (string (result?code)) (string (result?reason)))
