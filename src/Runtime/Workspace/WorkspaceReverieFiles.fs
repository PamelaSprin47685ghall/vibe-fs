module Wanxiangshu.Runtime.WorkspaceReverieFiles

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.WorkspacePathResolution

module Dyn = Wanxiangshu.Runtime.Dyn

[<Import("promises", "node:fs")>]
let private fsPromises: obj = jsNative

let maxReverieFileBytes = 1_048_576

type ReverieFileResult =
    { filePath: string
      content: string option
      skipReason: string option }

let private readdir (dir: string) : JS.Promise<obj[]> =
    fsPromises?readdir (dir, {| withFileTypes = true |})

let private stat (path: string) : JS.Promise<obj> = fsPromises?stat (path)
let private readFile (path: string) : JS.Promise<string> = fsPromises?readFile (path, "utf-8")
let private statSize (s: obj) : int = s?size
let private statIsFile (s: obj) : bool = s?isFile ()
let private statIsDirectory (s: obj) : bool = s?isDirectory ()

let private entryName (entry: obj) : string = entry?name
let private entryIsFile (entry: obj) : bool = entry?isFile ()
let private entryIsDirectory (entry: obj) : bool = entry?isDirectory ()

let readOne (cwd: string) (file: string) : JS.Promise<ReverieFileResult> =
    promise {
        let absolute = resolveAbsPath cwd file

        try
            let! s = stat absolute

            if not (statIsFile s) then
                return
                    { filePath = file
                      content = None
                      skipReason = Some "not-file" }
            elif statSize s > maxReverieFileBytes then
                return
                    { filePath = file
                      content = None
                      skipReason = Some "too-large" }
            else
                let! content = readFile absolute

                if Dyn.isNullish content || not (Dyn.typeIs content "string") then
                    return
                        { filePath = file
                          content = None
                          skipReason = Some "unreadable" }
                else
                    return
                        { filePath = absolute
                          content = Some content
                          skipReason = None }
        with _ ->
            return
                { filePath = file
                  content = None
                  skipReason = Some "unreadable" }
    }

let private readAndPrepend
    (cwd: string)
    (xs: ReverieFileResult list)
    (file: string)
    : JS.Promise<ReverieFileResult list> =
    promise {
        let! r = readOne cwd file
        return r :: xs
    }

let rec private foldAsync<'State, 'T>
    (folder: 'State -> 'T -> JS.Promise<'State>)
    (state: 'State)
    (items: 'T list)
    : JS.Promise<'State> =
    promise {
        match items with
        | [] -> return state
        | x :: xs ->
            let! nextState = folder state x
            return! foldAsync folder nextState xs
    }

let readReverieFiles (cwd: string) (files: string list) : JS.Promise<ReverieFileResult list> =
    promise {
        let! acc = foldAsync (readAndPrepend cwd) ([]: ReverieFileResult list) files
        return List.rev acc
    }
