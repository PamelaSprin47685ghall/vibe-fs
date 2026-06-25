// Caps file lists are cached per (sessionID, directory) on RuntimeScope.CapsFiles.
// Concurrent misses for the same key share one in-flight load via GetOrLoadCapsInflight.
module VibeFs.Shell.CapsFileCache

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.CapsFormat
open VibeFs.Shell.FileSys
open VibeFs.Shell.WorkspaceFiles
open VibeFs.Shell.RuntimeScope

[<Global("process")>]
let private nodeProcess : obj = jsNative

let private normalizeDirectory (directory: string) : string =
    let raw =
        if System.String.IsNullOrWhiteSpace directory then "."
        else directory
    resolve (nodeProcess?cwd()) raw

let private cacheKey (sessionID: string) (directory: string) =
    sessionID + "\u0000" + normalizeDirectory directory

let getOrLoadCapsFilesForScope (scope: RuntimeScope) (sessionID: string) (directory: string) : JS.Promise<CapsFile list> =
    let key = cacheKey sessionID directory
    let loadDir = normalizeDirectory directory
    match scope.TryGetCapsFiles key with
    | Some files -> Promise.lift files
    | None ->
        scope.GetOrLoadCapsInflight(key, fun () ->
            promise {
                let! files = findCapsFiles loadDir
                scope.AddCapsFilesIfAbsent(key, files)
                return files
            })