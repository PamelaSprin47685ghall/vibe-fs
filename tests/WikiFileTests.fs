module VibeFs.Tests.WikiFileTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Kernel.Wiki
open VibeFs.Shell.WikiFiles

[<Import("createRequire", "node:module")>]
let private createRequire (url: string) : (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta : obj = jsNative

let private requireFn = createRequire (string importMeta?url)
let private fsSync = requireFn "fs"
let private pathModule = requireFn "path"

let private readSync (p: string) : string = unbox (fsSync?readFileSync(p, "utf-8"))
let private existsSync (p: string) : bool = unbox (fsSync?existsSync(p))
let private readdirSync (p: string) : string array = unbox (fsSync?readdirSync(p))

let private entry (idStr: string) (q: string) (a: string) : WikiEntry =
    { id = (match tryParseId idStr with Some x -> x | None -> failwith "bad id"); q = q; a = a }

let private some (o: 'a option) : 'a =
    match o with Some v -> v | None -> failwith "expected Some"

let private joinPath (a: string) (b: string) : string = unbox (pathModule?join(a, b))

let emptyWikiProjectionSpec () = async {
    let! ws = mkdtempAsync "wiki-files-empty-" |> Async.AwaitPromise
    let! proj = readProjection ws |> Async.AwaitPromise
    check "empty wiki projection is empty" (Map.isEmpty proj)
    let! files = readWikiFiles ws |> Async.AwaitPromise
    check "empty wiki files is []" files.IsEmpty
    let! days = listDayFiles ws |> Async.AwaitPromise
    check "empty wiki listDayFiles is []" days.IsEmpty
    do! rmAsync ws |> Async.AwaitPromise
}

let appendCreatesDayFileSpec () = async {
    let! ws = mkdtempAsync "wiki-files-append-" |> Async.AwaitPromise
    do! ensureTodayFile ws "2026-06-19" |> Async.AwaitPromise
    let dayFile = dayPath ws "2026-06-19"
    check "append creates day file exists" (existsSync dayFile)
    let headerContent = readSync dayFile
    check "append day header contains rewritten" (headerContent.Contains "rewritten")
    check "append day header contains date" (headerContent.Contains "2026-06-19")
    do! appendEntries ws "2026-06-19" [ entry "0a3f" "q1" "a1" ] |> Async.AwaitPromise
    let afterContent = readSync dayFile
    check "append day has header line" (afterContent.Contains "wiki_header")
    check "append day has entry with id" (afterContent.Contains "0a3f")
    do! rmAsync ws |> Async.AwaitPromise
}

let appendMultipleKeepsNdjsonSpec () = async {
    let! ws = mkdtempAsync "wiki-files-ndjson-" |> Async.AwaitPromise
    do! ensureTodayFile ws "2026-06-19" |> Async.AwaitPromise
    do! appendEntries ws "2026-06-19" [ entry "0a3f" "q1" "a1" ] |> Async.AwaitPromise
    do! appendEntries ws "2026-06-19" [ entry "b912" "q2" "a2" ] |> Async.AwaitPromise
    let dayFile = dayPath ws "2026-06-19"
    let content = readSync dayFile
    let nonEmpty = content.Split('\n') |> Array.filter (fun l -> l.Trim() <> "")
    check "ndjson 1 header + 2 entries = 3 lines" (nonEmpty.Length = 3)
    do! rmAsync ws |> Async.AwaitPromise
}

let rewriteDayReplacesEntriesSpec () = async {
    let! ws = mkdtempAsync "wiki-files-rewrite-day-" |> Async.AwaitPromise
    do! ensureTodayFile ws "2026-06-19" |> Async.AwaitPromise
    do! appendEntries ws "2026-06-19" [ entry "0a3f" "oldq" "olda" ] |> Async.AwaitPromise
    do! rewriteDay ws "2026-06-19" [ entry "1111" "newq" "newa" ] |> Async.AwaitPromise
    let dayFile = dayPath ws "2026-06-19"
    let content = readSync dayFile
    check "rewrite day contains newq" (content.Contains "newq")
    check "rewrite day not contains oldq" (not (content.Contains "oldq"))
    check "rewrite day contains rewritten true" (content.Contains "\"rewritten\":true")
    do! rmAsync ws |> Async.AwaitPromise
}

let rewriteSnapshotReplacesHeaderSpec () = async {
    let! ws = mkdtempAsync "wiki-files-rewrite-snap-" |> Async.AwaitPromise
    do! ensureWikiDir ws |> Async.AwaitPromise
    let snapFile = snapshotPath ws
    do! writeFileAsync snapFile (renderHeader (SnapshotHeader(Some "2026-06-07")) + "\n") |> Async.AwaitPromise
    do! rewriteSnapshot ws "2026-06-14" [ entry "2222" "sq" "sa" ] |> Async.AwaitPromise
    let content = readSync snapFile
    check "rewrite snapshot contains new through" (content.Contains "2026-06-14")
    check "rewrite snapshot contains entry id" (content.Contains "2222")
    check "rewrite snapshot not contains old through" (not (content.Contains "2026-06-07"))
    do! rmAsync ws |> Async.AwaitPromise
}

let weeklyDeleteDayFilesSpec () = async {
    let! ws = mkdtempAsync "wiki-files-weekly-delete-" |> Async.AwaitPromise
    do! ensureTodayFile ws "2026-06-10" |> Async.AwaitPromise
    do! appendEntries ws "2026-06-10" [ entry "0a3f" "q1" "a1" ] |> Async.AwaitPromise
    do! ensureTodayFile ws "2026-06-12" |> Async.AwaitPromise
    do! appendEntries ws "2026-06-12" [ entry "b912" "q2" "a2" ] |> Async.AwaitPromise
    do! ensureTodayFile ws "2026-06-15" |> Async.AwaitPromise
    do! appendEntries ws "2026-06-15" [ entry "7c01" "q3" "a3" ] |> Async.AwaitPromise
    do! deleteDayFilesThrough ws "2026-06-12" |> Async.AwaitPromise
    check "weekly delete removes 06-10" (not (existsSync (dayPath ws "2026-06-10")))
    check "weekly delete removes 06-12" (not (existsSync (dayPath ws "2026-06-12")))
    check "weekly delete keeps 06-15" (existsSync (dayPath ws "2026-06-15"))
    do! rmAsync ws |> Async.AwaitPromise
}

let tempRenameCompletenessSpec () = async {
    let! ws = mkdtempAsync "wiki-files-tmp-rename-" |> Async.AwaitPromise
    do! rewriteDay ws "2026-06-19" [ entry "3333" "tq" "ta" ] |> Async.AwaitPromise
    let dayFile = dayPath ws "2026-06-19"
    check "temp rename leaves day file" (existsSync dayFile)
    let content = readSync dayFile
    check "temp rename content readable" (content.Contains "3333")
    let wDir = wikiDir ws
    let entries = readdirSync wDir
    check "temp rename no tmp file left" (not (entries |> Array.exists (fun f -> f.EndsWith ".tmp")))
    do! rmAsync ws |> Async.AwaitPromise
}

let listDayFilesSortedSpec () = async {
    let! ws = mkdtempAsync "wiki-files-list-sorted-" |> Async.AwaitPromise
    do! ensureTodayFile ws "2026-06-15" |> Async.AwaitPromise
    do! ensureTodayFile ws "2026-06-10" |> Async.AwaitPromise
    do! ensureTodayFile ws "2026-06-12" |> Async.AwaitPromise
    let! days = listDayFiles ws |> Async.AwaitPromise
    equal "listDayFiles sorted ascending" [ "2026-06-10"; "2026-06-12"; "2026-06-15" ] days
    do! rmAsync ws |> Async.AwaitPromise
}

let readProjectionLatestWinsSpec () = async {
    let! ws = mkdtempAsync "wiki-files-latest-wins-" |> Async.AwaitPromise
    do! ensureWikiDir ws |> Async.AwaitPromise
    let snapFile = snapshotPath ws
    do! writeFileAsync snapFile (renderNdjson (SnapshotHeader(Some "2026-06-07")) [ entry "0a3f" "oldq" "olda" ]) |> Async.AwaitPromise
    let dayFile = dayPath ws "2026-06-19"
    do! writeFileAsync dayFile (renderNdjson (DayHeader("2026-06-19", false)) [ entry "0a3f" "newq" "newa" ]) |> Async.AwaitPromise
    let! proj = readProjection ws |> Async.AwaitPromise
    let id = some (tryParseId "0a3f")
    match Map.tryFind id proj with
    | Some e ->
        check "readProjection latest wins q" (e.q = "newq")
        check "readProjection latest wins a" (e.a = "newa")
    | None -> check "readProjection latest wins found" false
    do! rmAsync ws |> Async.AwaitPromise
}

let run () : JS.Promise<unit> =
    async {
        do! emptyWikiProjectionSpec ()
        do! appendCreatesDayFileSpec ()
        do! appendMultipleKeepsNdjsonSpec ()
        do! rewriteDayReplacesEntriesSpec ()
        do! rewriteSnapshotReplacesHeaderSpec ()
        do! weeklyDeleteDayFilesSpec ()
        do! tempRenameCompletenessSpec ()
        do! listDayFilesSortedSpec ()
        do! readProjectionLatestWinsSpec ()
    }
    |> Async.StartAsPromise
