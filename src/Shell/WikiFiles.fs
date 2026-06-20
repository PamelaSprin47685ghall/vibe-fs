module VibeFs.Shell.WikiFiles

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Wiki

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

let private asPromise<'T> (o: obj) : JS.Promise<'T> = unbox o

let wikiDir (workspaceRoot: string) : string = join workspaceRoot "wiki"

let snapshotPath (workspaceRoot: string) : string = join (wikiDir workspaceRoot) "snapshot.ndjson"

let dayPath (workspaceRoot: string) (date: string) : string = join (wikiDir workspaceRoot) (date + ".ndjson")

let ensureWikiDir (workspaceRoot: string) : JS.Promise<unit> =
    async {
        try
            do! (fsPromises?mkdir(wikiDir workspaceRoot, {| recursive = true |}) |> asPromise<unit>) |> Async.AwaitPromise
        with _ -> ()
    }
    |> Async.StartAsPromise

let readWikiFiles (workspaceRoot: string) : JS.Promise<WikiFile list> =
    async {
        let wDir = wikiDir workspaceRoot
        if not (existsSync wDir) then return []
        else
            let! entries = (fsPromises?readdir(wDir) |> asPromise<obj array>) |> Async.AwaitPromise
            let names = entries |> Array.map string |> Array.filter (fun n -> n.EndsWith ".ndjson") |> Array.toList
            let dayNames = names |> List.filter ((<>) "snapshot.ndjson") |> List.sort
            let orderedNames = (if names |> List.contains "snapshot.ndjson" then [ "snapshot.ndjson" ] else []) @ dayNames
            let! texts =
                orderedNames
                |> List.map (fun n ->
                    async {
                        let p = join wDir n
                        let! t = (fsPromises?readFile(p, "utf-8") |> asPromise<string>) |> Async.AwaitPromise
                        return (n, t)
                    })
                |> Async.Parallel
            return
                texts
                |> Array.toList
                |> List.choose (fun (n, t) -> match parseNdjson n t with Ok f -> Some f | Error _ -> None)
    }
    |> Async.StartAsPromise

let readProjection (workspaceRoot: string) : JS.Promise<WikiProjection> =
    async {
        let! files = readWikiFiles workspaceRoot |> Async.AwaitPromise
        return projectLatestWins files
    }
    |> Async.StartAsPromise

let ensureTodayFile (workspaceRoot: string) (today: string) : JS.Promise<unit> =
    async {
        do! ensureWikiDir workspaceRoot |> Async.AwaitPromise
        let p = dayPath workspaceRoot today
        if not (existsSync p) then
            do! (fsPromises?writeFile(p, renderHeader (DayHeader(today, false)) + "\n", "utf-8") |> asPromise<unit>) |> Async.AwaitPromise
    }
    |> Async.StartAsPromise

let appendEntries (workspaceRoot: string) (today: string) (entries: WikiEntry list) : JS.Promise<unit> =
    async {
        if not entries.IsEmpty then
            do! ensureTodayFile workspaceRoot today |> Async.AwaitPromise
            let p = dayPath workspaceRoot today
            let data = entries |> List.map (fun e -> renderEntry e + "\n") |> String.concat ""
            if data <> "" then
                do! (fsPromises?appendFile(p, data, "utf-8") |> asPromise<unit>) |> Async.AwaitPromise
    }
    |> Async.StartAsPromise

let rewriteDay (workspaceRoot: string) (date: string) (entries: WikiEntry list) : JS.Promise<unit> =
    async {
        do! ensureWikiDir workspaceRoot |> Async.AwaitPromise
        let p = dayPath workspaceRoot date
        let tmp = p + ".tmp"
        let content = renderNdjson (DayHeader(date, true)) entries
        do! (fsPromises?writeFile(tmp, content, "utf-8") |> asPromise<unit>) |> Async.AwaitPromise
        do! (fsPromises?rename(tmp, p) |> asPromise<unit>) |> Async.AwaitPromise
    }
    |> Async.StartAsPromise

let rewriteSnapshot (workspaceRoot: string) (through: string) (entries: WikiEntry list) : JS.Promise<unit> =
    async {
        do! ensureWikiDir workspaceRoot |> Async.AwaitPromise
        let p = snapshotPath workspaceRoot
        let tmp = p + ".tmp"
        let content = renderNdjson (SnapshotHeader(Some through)) entries
        do! (fsPromises?writeFile(tmp, content, "utf-8") |> asPromise<unit>) |> Async.AwaitPromise
        do! (fsPromises?rename(tmp, p) |> asPromise<unit>) |> Async.AwaitPromise
    }
    |> Async.StartAsPromise

let listDayFiles (workspaceRoot: string) : JS.Promise<string list> =
    async {
        let wDir = wikiDir workspaceRoot
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
    |> Async.StartAsPromise

let deleteDayFilesThrough (workspaceRoot: string) (through: string) : JS.Promise<unit> =
    async {
        let! days = listDayFiles workspaceRoot |> Async.AwaitPromise
        let toDelete = days |> List.filter (fun d -> d <= through)
        for d in toDelete do
            do! (fsPromises?unlink(dayPath workspaceRoot d) |> asPromise<unit>) |> Async.AwaitPromise
    }
    |> Async.StartAsPromise