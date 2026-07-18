module Wanxiangshu.Runtime.WorkspaceFilesCollect

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.WorkspacePathResolution
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Runtime.Dyn

let maxFileSize = 4 * 1_048_576

type Budget =
    { results: CapsFile ResizeArray
      totalBytes: int
      count: int }

let fresh () : Budget =
    { results = ResizeArray()
      totalBytes = 0
      count = 0 }

let isFull (budget: Budget) : bool =
    budget.count >= 2000 || budget.totalBytes >= (8 * 1_048_576)

let absorb (file: CapsFile) (budget: Budget) : Budget =
    let nextTotal = budget.totalBytes + file.content.Length

    if nextTotal > (8 * 1_048_576) then
        budget
    else
        budget.results.Add file

        { results = budget.results
          totalBytes = nextTotal
          count = budget.count + 1 }

[<Import("promises", "node:fs")>]
let private fsPromises: obj = jsNative

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

let tryReadFileAsync (filePath: string) (label: string) : JS.Promise<CapsFile option> =
    promise {
        try
            let! s = stat filePath

            if not (statIsFile s) || statSize s > maxFileSize then
                return None
            else
                let! content = readFile filePath

                if isNullish content || not (typeIs content "string") then
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

let private absorbFileAsync (filePath: string) (label: string) (budget: Budget) : JS.Promise<Budget> =
    promise {
        if isFull budget then
            return budget
        else
            let! fileInfo = tryReadFileAsync filePath label

            match fileInfo with
            | None -> return budget
            | Some file -> return absorb file budget
    }

let rec private collectDirAsync (dirPath: string) (projectRoot: string) (budget: Budget) : JS.Promise<Budget> =
    promise {
        if isFull budget then
            return budget
        else
            try
                let! entries = readdir dirPath

                let rec processEntry index currentBudget =
                    promise {
                        if index >= entries.Length || isFull currentBudget then
                            return currentBudget
                        else
                            let entry = entries.[index]
                            let name = entryName entry
                            let fullPath = joinPath dirPath name

                            if entryIsFile entry then
                                let! nextBudget =
                                    absorbFileAsync fullPath (relativeToRoot projectRoot fullPath) currentBudget

                                return! processEntry (index + 1) nextBudget
                            elif entryIsDirectory entry then
                                let! nextBudget = collectDirAsync fullPath projectRoot currentBudget
                                return! processEntry (index + 1) nextBudget
                            else
                                return! processEntry (index + 1) currentBudget
                    }

                return! processEntry 0 budget
            with _ ->
                return budget
    }

let private readImportEntryAsync (projectRoot: string) (entry: string) (budget: Budget) : JS.Promise<Budget> =
    promise {
        if isFull budget then
            return budget
        else
            let absPath = resolveAbsPath projectRoot entry

            try
                let! s = stat absPath

                if statIsFile s then
                    return! absorbFileAsync absPath (relativeToRoot projectRoot absPath) budget
                elif statIsDirectory s then
                    return! collectDirAsync absPath projectRoot budget
                else
                    return budget
            with _ ->
                return budget
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

let readImportsAsync (projectRoot: string) (entries: string list) (budget: Budget) : JS.Promise<Budget> =
    foldAsync (fun currentBudget entry -> readImportEntryAsync projectRoot entry currentBudget) budget entries
