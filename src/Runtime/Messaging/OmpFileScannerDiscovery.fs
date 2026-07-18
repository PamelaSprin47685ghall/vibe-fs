module Wanxiangshu.Runtime.OmpFileScannerDiscovery

open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn

type OmpCapsFile =
    { filePath: string
      label: string
      content: string }

let internal capsDirRe = Regex("^[A-Z][A-Z0-9_]*$")

let private excludedDirNames =
    Set.ofList
        [ "AGENTS"
          "CLAUDE"
          "NODE_MODULES"
          ".GIT"
          "TARGET"
          "DIST"
          "OUT"
          ".VENV"
          "VENV"
          "__PYCACHE__"
          ".CACHE"
          ".NEXT"
          ".TURBO"
          ".PARCEL-CACHE" ]

let private maxDirDepth = 5
let private maxFileSize = 1_048_576

[<Import("promises", "node:fs")>]
let private fsPromises: obj = jsNative

[<Import("join", "node:path")>]
let internal pathJoin (a: string) (b: string) : string = jsNative

[<Import("relative", "node:path")>]
let internal pathRelative (root: string) (full: string) : string = jsNative

let internal readdir (dir: string) : JS.Promise<obj[]> =
    fsPromises?readdir (dir, {| withFileTypes = true |})

let private stat (path: string) : JS.Promise<obj> = fsPromises?stat (path)
let private readFile (path: string) : JS.Promise<string> = fsPromises?readFile (path, "utf-8")
let private realpath (path: string) : JS.Promise<string> = fsPromises?realpath (path)

let internal entryName (e: obj) : string = e?name
let internal entryIsFile (e: obj) : bool = e?isFile ()
let internal entryIsDirectory (e: obj) : bool = e?isDirectory ()
let private statIsFile (s: obj) : bool = s?isFile ()
let private statSize (s: obj) : int = s?size

let internal isExcludedDir (name: string) =
    let upper = name.ToUpperInvariant()

    excludedDirNames.Contains upper
    || (name.StartsWith "." && not (capsDirRe.IsMatch name))

let internal tryReadOmpFileAsync (filePath: string) (label: string) : JS.Promise<OmpCapsFile option> =
    promise {
        try
            let! s = stat filePath

            if not (statIsFile s) || statSize s > maxFileSize then
                return None
            else
                let! content = readFile filePath

                if
                    System.String.IsNullOrWhiteSpace content
                    || isNullish content
                    || not (typeIs content "string")
                then
                    return None
                else
                    return
                        Some
                            { filePath = filePath
                              label = label
                              content = content }
        with _ ->
            return None
    }

let rec internal discoverFilesInDirAsync
    (dirPath: string)
    (depth: int)
    (visited: Set<string>)
    : JS.Promise<string list> =
    promise {
        if depth >= maxDirDepth then
            return []
        else
            try
                let! real = realpath dirPath

                if visited.Contains real then
                    return []
                else
                    let visited' = visited.Add real
                    let! entries = readdir dirPath
                    let mutable files = []

                    for entry in entries do
                        let name = entryName entry
                        let full = pathJoin dirPath name

                        if entryIsFile entry then
                            files <- full :: files
                        elif entryIsDirectory entry && not (isExcludedDir name) then
                            let! nested = discoverFilesInDirAsync full (depth + 1) visited'
                            files <- List.append nested files

                    return List.rev files
            with _ ->
                return []
    }
