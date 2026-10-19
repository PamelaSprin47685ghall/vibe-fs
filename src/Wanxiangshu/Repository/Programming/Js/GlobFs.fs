namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

/// Deterministic gitignore-aware glob, bridged to `wanxiangli/fs` (CUT-24).
/// The enumeration algorithm lives in the library; this module keeps the
/// main-repo signature stable.
module JsGlobFs =

    [<Import("Filesystem", "wanxiangli/fs")>]
    let private Filesystem: obj = jsNative

    [<Emit("$0({ root: $1 })")>]
    let private createFilesystem (factory: obj) (root: string) : obj = jsNative

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

    /// JS-007 / [057]: one-directory listing with the library's gitignore
    /// semantics. The library resolves the path inside the root and refuses
    /// boundary escapes itself.
    let visibleEntries (root: string) (path: string) : Result<JsVisibleEntry list, JsFailure> =
        let api = createFilesystem Filesystem root
        let result: obj = api?visibleEntries (path)

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
