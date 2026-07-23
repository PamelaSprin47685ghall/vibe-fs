// Caps file lists are cached per (sessionID, directory) on RuntimeScope.CapsFiles.
// Concurrent misses for the same key share one in-flight load via GetOrLoadCapsInflight.
module Wanxiangshu.Runtime.CapsFileCache

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.FileSys
open Wanxiangshu.Runtime.WorkspaceFiles
open Wanxiangshu.Runtime.RuntimeScope

type CapsFileReadEntry =
    { path: string
      callId: string
      input: {| path: string |}
      output:
          {| success: bool
             file_size: int
             modifiedTime: string
             lines_read: int
             content: string |} }

let buildCapsFileReadData (projectRoot: string) : JS.Promise<CapsFileReadEntry[]> =
    promise {
        let! files = findCapsFiles projectRoot

        if List.isEmpty files then
            return [||]
        else
            let timestamp = getTimestampMs ()
            let token = string timestamp

            let modified =
                System.DateTimeOffset
                    .FromUnixTimeMilliseconds(timestamp)
                    .UtcDateTime.ToString("O")

            return
                files
                |> List.toArray
                |> Array.mapi (fun index f ->
                    let lines = f.content.Split('\n')

                    { path = f.label
                      callId = $"caps-fr-{token}-{index}"
                      input = {| path = f.label |}
                      output =
                        {| success = true
                           file_size = f.content.Length
                           modifiedTime = modified
                           lines_read = lines.Length
                           content = lines |> Array.mapi (fun i line -> $"{i + 1}\t{line}") |> String.concat "\n" |} })
    }

type private INodeProcess =
    abstract cwd: unit -> string

[<Global("globalThis.process")>]
let private nodeProcess: INodeProcess = jsNative

let private normalizeDirectory (directory: string) : string =
    let raw =
        if System.String.IsNullOrWhiteSpace directory then
            "."
        else
            directory

    resolve (nodeProcess.cwd ()) raw

let private cacheKey (sessionID: string) (directory: string) =
    sessionID + "\u0000" + normalizeDirectory directory

let getOrLoadCapsFilesForScope
    (scope: RuntimeScope)
    (sessionID: string)
    (directory: string)
    : JS.Promise<CapsFile list> =
    let key = cacheKey sessionID directory
    let loadDir = normalizeDirectory directory

    match scope.TryGetCapsFiles key with
    | Some files -> Promise.lift files
    | None ->
        scope.GetOrLoadCapsInflight(
            key,
            fun () ->
                promise {
                    let! files = findCapsFiles loadDir
                    scope.AddCapsFilesIfAbsent(key, files)
                    return files
                }
        )

/// Drop per-session caps file cache so the next load re-reads the workspace.
/// Prefix matches CapsCache keys: sessionID + "\u0000" + directory.
let invalidateCapsFilesForSession (scope: RuntimeScope) (sessionID: string) : unit =
    if sessionID = "" then
        ()
    else
        let prefix = sessionID + "\u0000"
        scope.ClearCapsFilesForSession prefix
        scope.ClearCapsInflightForSession prefix
