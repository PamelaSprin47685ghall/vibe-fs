module Wanxiangshu.Runtime.OmpFileScanner

open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OmpFileScannerDiscovery

type OmpCapsFile = OmpFileScannerDiscovery.OmpCapsFile

let private capsFileRe = Regex("^[A-Z][A-Z0-9_]*\\.md$")

let private excludedFileNames = Set.ofList [ "AGENTS.md"; "CLAUDE.md"; "README.md" ]

let private maxTotalContextBytes = 8 * 1_048_576
let private maxCapsFiles = 200

type private ScanBudget =
    { files: OmpCapsFile list
      totalBytes: int
      count: int }

let private scanFull (budget: ScanBudget) =
    budget.count >= maxCapsFiles || budget.totalBytes >= maxTotalContextBytes

let private absorbOmpFile (budget: ScanBudget) (file: OmpCapsFile) : ScanBudget =
    let nextBytes = budget.totalBytes + file.content.Length

    if nextBytes > maxTotalContextBytes then
        budget
    else
        { files = file :: budget.files
          totalBytes = nextBytes
          count = budget.count + 1 }

let private foldAsync<'T, 'S> (folder: 'S -> 'T -> JS.Promise<'S>) (state: 'S) (items: 'T list) : JS.Promise<'S> =
    promise {
        let mutable s = state

        for item in items do
            let! next = folder s item
            s <- next

        return s
    }

let findOmpCapsFiles (projectRoot: string) : JS.Promise<OmpCapsFile list> =
    promise {
        let mutable budget =
            { files = []
              totalBytes = 0
              count = 0 }

        try
            let! rootEntries = readdir projectRoot

            for entry in rootEntries do
                if scanFull budget then
                    ()
                else
                    let name = entryName entry
                    let fullPath = pathJoin projectRoot name

                    if
                        entryIsFile entry
                        && capsFileRe.IsMatch name
                        && not (excludedFileNames.Contains name)
                    then
                        let! info = tryReadOmpFileAsync fullPath name

                        match info with
                        | Some file -> budget <- absorbOmpFile budget file
                        | None -> ()
                    elif entryIsDirectory entry && capsDirRe.IsMatch name && not (isExcludedDir name) then
                        let! dirFiles = discoverFilesInDirAsync fullPath 0 Set.empty

                        let! budget' =
                            foldAsync
                                (fun b filePath ->
                                    promise {
                                        if scanFull b then
                                            return b
                                        else
                                            let! info =
                                                tryReadOmpFileAsync filePath (pathRelative projectRoot filePath)

                                            match info with
                                            | Some file -> return absorbOmpFile b file
                                            | None -> return b
                                    })
                                budget
                                dirFiles

                        budget <- budget'

            return budget.files |> List.sortBy (fun f -> f.filePath)
        with _ ->
            return []
    }
