module Wanxiangshu.Tests.KnowledgeGraphFileTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Kernel.KnowledgeGraph
open Wanxiangshu.Kernel.KnowledgeGraph.Types
open Wanxiangshu.Kernel.KnowledgeGraph.Codec
open Wanxiangshu.Shell.KnowledgeGraphFiles

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

let private entry (idStr: string) (entities: string list) (fact: string) : KnowledgeGraphEntry =
    { id = (match tryParseId idStr with Some x -> x | None -> failwith "bad id"); entity = entities; fact = fact }

let private some (o: 'a option) : 'a =
    match o with Some v -> v | None -> failwith "expected Some"

let private joinPath (a: string) (b: string) : string = unbox (pathModule?join(a, b))

let emptyKnowledgeGraphProjectionSpec () = promise {
    let! ws = mkdtempAsync "kg-files-empty-"
    let! proj = readProjection ws
    check "empty knowledge graph projection is empty" (Map.isEmpty proj)
    let! files = readKnowledgeGraphFiles ws
    check "empty knowledge graph files is []" files.IsEmpty
    let! days = listDayFiles ws
    check "empty knowledge graph listDayFiles is []" days.IsEmpty
    do! rmAsync ws
}

let appendCreatesDayFileSpec () = promise {
    let! ws = mkdtempAsync "kg-files-append-"
    do! ensureTodayFile ws "2026-06-19"
    let dayFile = dayPath ws "2026-06-19"
    check "append creates day file exists" (existsSync dayFile)
    let headerContent = readSync dayFile
    check "append day header contains rewritten" (headerContent.Contains "rewritten")
    check "append day header contains date" (headerContent.Contains "2026-06-19")
    do! appendEntries ws "2026-06-19" [ entry "0a3f" ["e1"] "f1" ]
    let afterContent = readSync dayFile
    check "append day has header line" (afterContent.Contains "knowledge_graph_header")
    check "append day has entry with id" (afterContent.Contains "0a3f")
    check "append day uses kg dir" ((knowledgeGraphDir ws).EndsWith "kg")
    do! rmAsync ws
}

let appendMultipleKeepsNdjsonSpec () = promise {
    let! ws = mkdtempAsync "kg-files-ndjson-"
    do! ensureTodayFile ws "2026-06-19"
    do! appendEntries ws "2026-06-19" [ entry "0a3f" ["e1"] "f1" ]
    do! appendEntries ws "2026-06-19" [ entry "b912" ["e2"] "f2" ]
    let dayFile = dayPath ws "2026-06-19"
    let content = readSync dayFile
    let nonEmpty = content.Split('\n') |> Array.filter (fun l -> l.Trim() <> "")
    check "ndjson 1 header + 2 entries = 3 lines" (nonEmpty.Length = 3)
    do! rmAsync ws
}

let rewriteDayReplacesEntriesSpec () = promise {
    let! ws = mkdtempAsync "kg-files-rewrite-day-"
    do! ensureTodayFile ws "2026-06-19"
    do! appendEntries ws "2026-06-19" [ entry "0a3f" ["old e"] "old fact" ]
    do! rewriteDay ws "2026-06-19" [ entry "1111" ["new e"] "new fact" ]
    let dayFile = dayPath ws "2026-06-19"
    let content = readSync dayFile
    check "rewrite day contains new entity" (content.Contains "new e")
    check "rewrite day contains new fact" (content.Contains "new fact")
    check "rewrite day not contains old entity" (not (content.Contains "old e"))
    check "rewrite day contains rewritten true" (content.Contains "\"rewritten\":true")
    do! rmAsync ws
}

let tempRenameCompletenessSpec () = promise {
    let! ws = mkdtempAsync "kg-files-tmp-rename-"
    do! rewriteDay ws "2026-06-19" [ entry "3333" ["e"] "f" ]
    let dayFile = dayPath ws "2026-06-19"
    check "temp rename leaves day file" (existsSync dayFile)
    let content = readSync dayFile
    check "temp rename content readable" (content.Contains "3333")
    let kDir = knowledgeGraphDir ws
    check "temp rename uses kg directory" (kDir.EndsWith "kg")
    let entries = readdirSync kDir
    check "temp rename no tmp file left" (not (entries |> Array.exists (fun f -> f.EndsWith ".tmp")))
    do! rmAsync ws
}

let listDayFilesSortedSpec () = promise {
    let! ws = mkdtempAsync "kg-files-list-sorted-"
    do! ensureTodayFile ws "2026-06-15"
    do! ensureTodayFile ws "2026-06-10"
    do! ensureTodayFile ws "2026-06-12"
    let! days = listDayFiles ws
    equal "listDayFiles sorted ascending" [ "2026-06-10"; "2026-06-12"; "2026-06-15" ] days
    do! rmAsync ws
}

let readProjectionLatestWinsSpec () = promise {
    let! ws = mkdtempAsync "kg-files-latest-wins-"
    do! ensureKnowledgeGraphDir ws
    do! writeFileAsync (dayPath ws "2026-06-18") (renderNdjson (DayHeader("2026-06-18", true)) [ entry "0a3f" ["old e"] "old fact" ])
    do! writeFileAsync (dayPath ws "2026-06-19") (renderNdjson (DayHeader("2026-06-19", false)) [ entry "0a3f" ["new e"] "new fact" ])
    let! proj = readProjection ws
    let id = some (tryParseId "0a3f")
    match Map.tryFind id proj with
    | Some e ->
        check "readProjection latest wins entity" (e.entity = ["new e"])
        check "readProjection latest wins fact" (e.fact = "new fact")
    | None -> check "readProjection latest wins found" false
    do! rmAsync ws
}

let run () : JS.Promise<unit> =
    promise {
        do! emptyKnowledgeGraphProjectionSpec ()
        do! appendCreatesDayFileSpec ()
        do! appendMultipleKeepsNdjsonSpec ()
        do! rewriteDayReplacesEntriesSpec ()
        do! tempRenameCompletenessSpec ()
        do! listDayFilesSortedSpec ()
        do! readProjectionLatestWinsSpec ()
    }
