module VibeFs.Shell.WorkspaceFiles

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.CapsFormat

let maxFileSize = 4 * 1_048_576

type Budget = { results: CapsFile ResizeArray; totalBytes: int; count: int }

let fresh () : Budget = { results = ResizeArray (); totalBytes = 0; count = 0 }

let isFull (budget: Budget) : bool = budget.count >= 2000 || budget.totalBytes >= (8 * 1_048_576)

let absorb (file: CapsFile) (budget: Budget) : Budget =
    let nextTotal = budget.totalBytes + file.content.Length
    if nextTotal > (8 * 1_048_576) then budget
    else
        budget.results.Add file
        { results = budget.results; totalBytes = nextTotal; count = budget.count + 1 }

[<Import("parse", "yaml")>]
let private yamlParse (text: string) : obj = jsNative

[<Import("promises", "node:fs")>]
let private fsPromises : obj = jsNative

let private readdir (dir: string) : JS.Promise<obj[]> = fsPromises?readdir(dir, {| withFileTypes = true |})
let private stat (path: string) : JS.Promise<obj> = fsPromises?stat(path)
let private readFile (path: string) : JS.Promise<string> = fsPromises?readFile(path, "utf-8")
let private statSize (s: obj) : int = s?size
let private statIsFile (s: obj) : bool = s?isFile ()
let private statIsDirectory (s: obj) : bool = s?isDirectory ()

[<Import("join", "node:path")>]
let private pathJoin (a: string) (b: string) : string = jsNative
[<Import("relative", "node:path")>]
let private pathRelative (a: string) (b: string) : string = jsNative
[<Import("resolve", "node:path")>]
let private pathResolve (cwd: string) (file: string) : string = jsNative

let private entryName (entry: obj) : string = entry?name
let private entryIsFile (entry: obj) : bool = entry?isFile ()
let private entryIsDirectory (entry: obj) : bool = entry?isDirectory ()

let private tryReadFileAsync (filePath: string) (label: string) : JS.Promise<CapsFile option> =
    promise {
        try
            let! fileStat = stat filePath
            if not (statIsFile fileStat) || statSize fileStat > maxFileSize then return None
            else
                let! content = readFile filePath
                return if content.Trim() = "" then None else Some { filePath = filePath; label = label; content = content }
        with _ -> return None
    }

let private absorbFileAsync (filePath: string) (label: string) (budget: Budget) : JS.Promise<Budget> =
    promise {
        if isFull budget then return budget
        else
            let! fileInfo = tryReadFileAsync filePath label
            match fileInfo with
            | None -> return budget
            | Some file -> return absorb file budget
    }

let rec private collectDirAsync (dirPath: string) (projectRoot: string) (budget: Budget) : JS.Promise<Budget> =
    promise {
        if isFull budget then return budget
        else
            try
                let! entries = readdir dirPath
                let rec processEntry index currentBudget =
                    promise {
                        if index >= entries.Length || isFull currentBudget then return currentBudget
                        else
                            let entry = entries.[index]
                            let name = entryName entry
                            let fullPath = pathJoin dirPath name
                            if entryIsFile entry then
                                let! nextBudget = absorbFileAsync fullPath (pathRelative projectRoot fullPath) currentBudget
                                return! processEntry (index + 1) nextBudget
                            elif entryIsDirectory entry then
                                let! nextBudget = collectDirAsync fullPath projectRoot currentBudget
                                return! processEntry (index + 1) nextBudget
                            else
                                return! processEntry (index + 1) currentBudget
                    }
                return! processEntry 0 budget
            with _ -> return budget
    }

let private readImportEntryAsync (projectRoot: string) (entry: string) (budget: Budget) : JS.Promise<Budget> =
    promise {
        if isFull budget then return budget
        else
            let absPath = pathResolve projectRoot entry
            try
                let! s = stat absPath
                if statIsFile s then
                    return! absorbFileAsync absPath (pathRelative projectRoot absPath) budget
                elif statIsDirectory s then
                    return! collectDirAsync absPath projectRoot budget
                else
                    return budget
            with _ -> return budget
    }

let private readImportsAsync (projectRoot: string) (entries: string list) (budget: Budget) : JS.Promise<Budget> =
    let rec loop remaining currentBudget =
        promise {
            match remaining with
            | [] -> return currentBudget
            | entry :: rest ->
                if isFull currentBudget then return currentBudget
                else
                    let! nextBudget = readImportEntryAsync projectRoot entry currentBudget
                    return! loop rest nextBudget
        }
    loop entries budget

let private splitFrontMatter (content: string) : string * obj option =
    let trimmed = content.TrimStart('\r', '\n')
    if not (trimmed.StartsWith("---")) then (content, None)
    else
        let afterFirst = trimmed.[3..].TrimStart('\r', '\n')
        match afterFirst.IndexOf("---") with
        | -1 -> (content, None)
        | closeIdx ->
            let yamlText = afterFirst.[.. closeIdx - 1]
            let body = afterFirst.[closeIdx + 3 ..].TrimStart('\r', '\n')
            let fm =
                try
                    Some (yamlParse yamlText)
                with _ -> None
            (body, fm)

let private extractImportList (frontmatter: obj option) : string list =
    match frontmatter with
    | None -> []
    | Some fm ->
        if VibeFs.Kernel.Dyn.isNullish fm then []
        else
            let importVal = VibeFs.Kernel.Dyn.get fm "import"
            if VibeFs.Kernel.Dyn.isNullish importVal then []
            elif VibeFs.Kernel.Dyn.isArray importVal then importVal :?> obj array |> Array.map string |> List.ofArray
            else [ string importVal ]

let findCapsFiles (projectRoot: string) : JS.Promise<CapsFile list> =
    promise {
        let agentsPath = pathJoin projectRoot "AGENTS.md"
        let! agentsFile = tryReadFileAsync agentsPath "AGENTS.md"
        match agentsFile with
        | None -> return []
        | Some agents ->
            let body, fm = splitFrontMatter agents.content
            let importList = extractImportList fm
            let initial =
                if System.String.IsNullOrWhiteSpace body then fresh ()
                else absorb { filePath = agentsPath; label = "AGENTS.md"; content = body } (fresh ())
            let! finalBudget = readImportsAsync projectRoot importList initial
            return finalBudget.results |> Seq.toList |> List.sortBy (fun file -> file.filePath)
    }

let maxReverieFileBytes = 1_048_576

type ReverieFileResult =
    { filePath: string
      content: string option
      skipReason: string option }

let private statSize2 (s: obj) : int = s?size
let private statIsFile2 (s: obj) : bool = s?isFile ()

let readOne (cwd: string) (file: string) : JS.Promise<ReverieFileResult> =
    promise {
        let absolute = pathResolve cwd file
        try
            let! s = stat absolute
            if not (statIsFile2 s) then
                return { filePath = file; content = None; skipReason = Some "not-file" }
            elif statSize2 s > maxReverieFileBytes then
                return { filePath = file; content = None; skipReason = Some "too-large" }
            else
                let! content = readFile absolute
                return { filePath = absolute; content = Some content; skipReason = None }
        with _ ->
            return { filePath = file; content = None; skipReason = Some "unreadable" }
    }

let readReverieFiles (cwd: string) (files: string list) : JS.Promise<ReverieFileResult list> =
    let rec loop remaining acc =
        promise {
            match remaining with
            | [] -> return List.rev acc
            | file :: rest ->
                let! r = readOne cwd file
                return! loop rest (r :: acc)
        }
    loop files []
