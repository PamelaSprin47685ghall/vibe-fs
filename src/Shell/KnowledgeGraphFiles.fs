module VibeFs.Shell.KnowledgeGraphFiles

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Kernel.KnowledgeGraph.Codec

[<Import("promises", "node:fs")>]
let private fsPromises : obj = jsNative

[<Import("join", "node:path")>]
let private join (a: string) (b: string) : string = jsNative

[<Import("existsSync", "node:fs")>]
let private existsSync (p: string) : bool = jsNative

[<Import("mkdirSync", "node:fs")>]
let private mkdirSync (dir: string) (opts: obj) : unit = jsNative

[<Import("readdirSync", "node:fs")>]
let private readdirSync (p: string) : obj array = jsNative

let knowledgeGraphDir (workspaceRoot: string) : string = join workspaceRoot "kg"

let knowledgeGraphDirExists (workspaceRoot: string) : bool = existsSync (knowledgeGraphDir workspaceRoot)

let dayPath (workspaceRoot: string) (date: string) : string = join (knowledgeGraphDir workspaceRoot) (date + ".ndjson")

let ensureKnowledgeGraphDir (workspaceRoot: string) : JS.Promise<unit> =
    promise {
        try
            do! fsPromises?mkdir(knowledgeGraphDir workspaceRoot, {| recursive = true |})
        with _ -> ()
    }

let readKnowledgeGraphFiles (workspaceRoot: string) : JS.Promise<KnowledgeGraphFile list> =
    promise {
        let wDir = knowledgeGraphDir workspaceRoot
        if not (existsSync wDir) then return []
        else
            let! (entries: obj[]) = fsPromises?readdir(wDir)
            let orderedNames = entries |> Array.map string |> Array.filter (fun n -> n.EndsWith ".ndjson" && n <> "snapshot.ndjson") |> Array.sort
            let! texts =
                orderedNames
                |> Array.toList
                |> List.map (fun n ->
                    promise {
                        let p = join wDir n
                        let! (t: string) = fsPromises?readFile(p, "utf-8")
                        return (n, t)
                    })
                |> Promise.all
            return
                texts
                |> Array.toList
                |> List.choose (fun (n, t) -> match parseNdjson n t with Ok f -> Some f | Error _ -> None)
    }

let readProjection (workspaceRoot: string) : JS.Promise<KnowledgeGraphProjection> =
    promise {
        let! files = readKnowledgeGraphFiles workspaceRoot
        return projectLatestWins files
    }

let ensureTodayFile (workspaceRoot: string) (today: string) : JS.Promise<unit> =
    promise {
        do! ensureKnowledgeGraphDir workspaceRoot
        let p = dayPath workspaceRoot today
        if not (existsSync p) then
            do! fsPromises?writeFile(p, renderHeader (DayHeader(today, false)) + "\n", "utf-8")
    }

let appendEntries (workspaceRoot: string) (today: string) (entries: KnowledgeGraphEntry list) : JS.Promise<unit> =
    promise {
        if not entries.IsEmpty then
            do! ensureTodayFile workspaceRoot today
            let p = dayPath workspaceRoot today
            let data = entries |> List.map (fun e -> renderEntry e + "\n") |> String.concat ""
            if data <> "" then
                do! fsPromises?appendFile(p, data, "utf-8")
    }

let dayEntryCount (workspaceRoot: string) (today: string) : JS.Promise<int> =
    promise {
        let p = dayPath workspaceRoot today
        if not (existsSync p) then return 0
        else
            let! (t: string) = fsPromises?readFile(p, "utf-8")
            let name = today + ".ndjson"
            return match parseNdjson name t with Ok f -> f.entries.Length | Error _ -> 0
    }

let rewriteDay (workspaceRoot: string) (date: string) (entries: KnowledgeGraphEntry list) : JS.Promise<unit> =
    promise {
        do! ensureKnowledgeGraphDir workspaceRoot
        let p = dayPath workspaceRoot date
        let tmp = p + ".tmp"
        let content = renderNdjson (DayHeader(date, true)) entries
        do! fsPromises?writeFile(tmp, content, "utf-8")
        do! fsPromises?rename(tmp, p)
    }

let listDayFiles (workspaceRoot: string) : JS.Promise<string list> =
    promise {
        let wDir = knowledgeGraphDir workspaceRoot
        if not (existsSync wDir) then return []
        else
            let entries = readdirSync wDir
            return
                entries
                |> Array.map string
                |> Array.filter (fun n -> n.EndsWith ".ndjson" && n <> "snapshot.ndjson")
                |> Array.map (fun n -> n.Replace(".ndjson", ""))
                |> Array.toList
                |> List.sort
    }
