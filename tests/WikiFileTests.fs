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

let emptyWikiProjectionSpec () = promise {
    let! ws = mkdtempAsync "wiki-files-empty-"
    let! proj = readProjection ws
    check "empty wiki projection is empty" (Map.isEmpty proj)
    let! files = readWikiFiles ws
    check "empty wiki files is []" files.IsEmpty
    let! days = listDayFiles ws
    check "empty wiki listDayFiles is []" days.IsEmpty
    do! rmAsync ws
}

let appendCreatesDayFileSpec () = promise {
    let! ws = mkdtempAsync "wiki-files-append-"
    do! ensureTodayFile ws "2026-06-19"
    let dayFile = dayPath ws "2026-06-19"
    check "append creates day file exists" (existsSync dayFile)
    let headerContent = readSync dayFile
    check "append day header contains rewritten" (headerContent.Contains "rewritten")
    check "append day header contains date" (headerContent.Contains "2026-06-19")
    do! appendEntries ws "2026-06-19" [ entry "0a3f" "q1" "a1" ]
    let afterContent = readSync dayFile
    check "append day has header line" (afterContent.Contains "wiki_header")
    check "append day has entry with id" (afterContent.Contains "0a3f")
    do! rmAsync ws
}

let appendMultipleKeepsNdjsonSpec () = promise {
    let! ws = mkdtempAsync "wiki-files-ndjson-"
    do! ensureTodayFile ws "2026-06-19"
    do! appendEntries ws "2026-06-19" [ entry "0a3f" "q1" "a1" ]
    do! appendEntries ws "2026-06-19" [ entry "b912" "q2" "a2" ]
    let dayFile = dayPath ws "2026-06-19"
    let content = readSync dayFile
    let nonEmpty = content.Split('\n') |> Array.filter (fun l -> l.Trim() <> "")
    check "ndjson 1 header + 2 entries = 3 lines" (nonEmpty.Length = 3)
    do! rmAsync ws
}

let rewriteDayReplacesEntriesSpec () = promise {
    let! ws = mkdtempAsync "wiki-files-rewrite-day-"
    do! ensureTodayFile ws "2026-06-19"
    do! appendEntries ws "2026-06-19" [ entry "0a3f" "oldq" "olda" ]
    do! rewriteDay ws "2026-06-19" [ entry "1111" "newq" "newa" ]
    let dayFile = dayPath ws "2026-06-19"
    let content = readSync dayFile
    check "rewrite day contains newq" (content.Contains "newq")
    check "rewrite day not contains oldq" (not (content.Contains "oldq"))
    check "rewrite day contains rewritten true" (content.Contains "\"rewritten\":true")
    do! rmAsync ws
}

let tempRenameCompletenessSpec () = promise {
    let! ws = mkdtempAsync "wiki-files-tmp-rename-"
    do! rewriteDay ws "2026-06-19" [ entry "3333" "tq" "ta" ]
    let dayFile = dayPath ws "2026-06-19"
    check "temp rename leaves day file" (existsSync dayFile)
    let content = readSync dayFile
    check "temp rename content readable" (content.Contains "3333")
    let wDir = wikiDir ws
    let entries = readdirSync wDir
    check "temp rename no tmp file left" (not (entries |> Array.exists (fun f -> f.EndsWith ".tmp")))
    do! rmAsync ws
}

let listDayFilesSortedSpec () = promise {
    let! ws = mkdtempAsync "wiki-files-list-sorted-"
    do! ensureTodayFile ws "2026-06-15"
    do! ensureTodayFile ws "2026-06-10"
    do! ensureTodayFile ws "2026-06-12"
    let! days = listDayFiles ws
    equal "listDayFiles sorted ascending" [ "2026-06-10"; "2026-06-12"; "2026-06-15" ] days
    do! rmAsync ws
}

let readProjectionLatestWinsSpec () = promise {
    let! ws = mkdtempAsync "wiki-files-latest-wins-"
    do! ensureWikiDir ws
    do! writeFileAsync (dayPath ws "2026-06-18") (renderNdjson (DayHeader("2026-06-18", true)) [ entry "0a3f" "oldq" "olda" ])
    do! writeFileAsync (dayPath ws "2026-06-19") (renderNdjson (DayHeader("2026-06-19", false)) [ entry "0a3f" "newq" "newa" ])
    let! proj = readProjection ws
    let id = some (tryParseId "0a3f")
    match Map.tryFind id proj with
    | Some e ->
        check "readProjection latest wins q" (e.q = "newq")
        check "readProjection latest wins a" (e.a = "newa")
    | None -> check "readProjection latest wins found" false
    do! rmAsync ws
}

let run () : JS.Promise<unit> =
    promise {
        do! emptyWikiProjectionSpec ()
        do! appendCreatesDayFileSpec ()
        do! appendMultipleKeepsNdjsonSpec ()
        do! rewriteDayReplacesEntriesSpec ()
        do! tempRenameCompletenessSpec ()
        do! listDayFilesSortedSpec ()
        do! readProjectionLatestWinsSpec ()
    }
