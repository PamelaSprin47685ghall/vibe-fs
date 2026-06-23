module VibeFs.Tests.KnowledgeGraphTests

open System
open Fable.Core
open VibeFs.Tests.Assert
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraphPrompts

let private ok r =
    match r with
    | Ok v -> v
    | Error e -> failwithf "%A" e

let private some (o: 'a option) : 'a =
    match o with Some v -> v | None -> failwith "expected Some"

let private isOk r =
    match r with
    | Ok _ -> true
    | Error _ -> false

let private isErr r =
    match r with
    | Ok _ -> false
    | Error _ -> true

let private entry idStr entities fact : KnowledgeGraphEntry =
    { id = some (tryParseId idStr); entity = entities; fact = fact }

let private file (header: KnowledgeGraphHeader) (entries: KnowledgeGraphEntry list) : KnowledgeGraphFile = { header = header; entries = entries }

let private projection (entries: KnowledgeGraphEntry list) : KnowledgeGraphProjection =
    entries |> List.map (fun e -> e.id, e) |> Map.ofList

let idParseSpec () =
    check "tryParseId 0a3f Some" (tryParseId "0a3f" |> Option.isSome)
    check "tryParseId FFFF None (uppercase)" (tryParseId "FFFF" |> Option.isNone)
    check "tryParseId 0a3f5 None (5 chars)" (tryParseId "0a3f5" |> Option.isNone)
    check "tryParseId empty None" (tryParseId "" |> Option.isNone)
    check "tryParseId g123 None" (tryParseId "g123" |> Option.isNone)
    let id = match tryParseId "b912" with Some x -> x | None -> failwith "b912 should parse"
    equal "idValue round trips" "b912" (idValue id)

let headerParseSpec () =
    let dayLine = """{"type":"knowledge_graph_header","version":1,"kind":"day","date":"2026-06-19","rewritten":false}"""
    check "parseHeaderLine day Ok DayHeader" (match parseHeaderLine dayLine with Ok(DayHeader("2026-06-19", false)) -> true | _ -> false)
    check "parseHeaderLine garbage Error" (isErr (parseHeaderLine "not json at all"))
    let snapshotLine = """{"type":"knowledge_graph_header","version":1,"kind":"snapshot","through":"2026-06-14"}"""
    check "parseHeaderLine snapshot rejected" (isErr (parseHeaderLine snapshotLine))

let headerRenderSpec () =
    let dayRendered = renderHeader (DayHeader("2026-06-18", true))
    check "renderHeader day contains rewritten true" (dayRendered.Contains("\"rewritten\":true"))
    check "renderHeader day contains kind day" (dayRendered.Contains("\"kind\":\"day\""))
    check "renderHeader day contains date" (dayRendered.Contains("2026-06-18"))
    let dh = DayHeader("2026-06-18", true)
    check "header round-trip day" (parseHeaderLine (renderHeader dh) = Ok dh)

let entryParseRenderSpec () =
    let line = """{"id":"0a3f","entity":["项目插件入口"],"fact":"src/Opencode/Plugin.fs"}"""
    let parsed = ok (parseEntryLine line)
    equal "parseEntry id" "0a3f" (idValue parsed.id)
    equal "parseEntry entity" ["项目插件入口"] parsed.entity
    equal "parseEntry fact" "src/Opencode/Plugin.fs" parsed.fact
    let rerendered = renderEntry parsed
    let reparsed = ok (parseEntryLine rerendered)
    equal "entry render round-trip id" (idValue parsed.id) (idValue reparsed.id)
    equal "entry render round-trip entity" parsed.entity reparsed.entity
    equal "entry render round-trip fact" parsed.fact reparsed.fact
    let missingFact = """{"id":"0a3f","entity":["项目插件入口"]}"""
    check "parseEntry missing fact Error" (isErr (parseEntryLine missingFact))
    let badId5 = """{"id":"0a3f5","entity":["项目插件入口"],"fact":"fact"}"""
    check "parseEntry bad id 5 chars Error" (isErr (parseEntryLine badId5))
    let badIdUpper = """{"id":"FFFF","entity":["项目插件入口"],"fact":"fact"}"""
    check "parseEntry bad id uppercase Error" (isErr (parseEntryLine badIdUpper))

let ndjsonParseSpec () =
    let dayHeader = """{"type":"knowledge_graph_header","version":1,"kind":"day","date":"2026-06-19","rewritten":false}"""
    let e1 = """{"id":"0a3f","entity":["e1"],"fact":"f1"}"""
    let e2 = """{"id":"b912","entity":["e2"],"fact":"f2"}"""
    let text = String.concat "\n" [ dayHeader; e1; e2; "" ]
    let parsed = ok (parseNdjson "f" text)
    check "parseNdjson 2 entries" (parsed.entries.Length = 2)
    equal "parseNdjson entry0 id" "0a3f" (idValue parsed.entries.[0].id)
    equal "parseNdjson entry1 id" "b912" (idValue parsed.entries.[1].id)
    check "parseNdjson garbage header Error" (isErr (parseNdjson "f" "garbage\n"))
    let badEntry = """not json"""
    let text2 = String.concat "\n" [ dayHeader; e1; badEntry; e2; "" ]
    let parsed2 = ok (parseNdjson "f" text2)
    check "parseNdjson truncates at first bad entry" (parsed2.entries.Length = 1)
    equal "parseNdjson truncates keeps good entry" "0a3f" (idValue parsed2.entries.[0].id)
    let emptyText = dayHeader + "\n"
    let parsed3 = ok (parseNdjson "f" emptyText)
    check "parseNdjson empty after header" (parsed3.entries.IsEmpty)

let ndjsonRenderSpec () =
    let dh = DayHeader("2026-06-19", false)
    let renderedEmpty = renderNdjson dh []
    check "renderNdjson empty ends with header line newline" (renderedEmpty.EndsWith(renderHeader dh + "\n"))
    let e1 = entry "0a3f" ["e1"] "f1"
    let e2 = entry "b912" ["e2"] "f2"
    let rendered = renderNdjson dh [ e1; e2 ]
    check "renderNdjson has trailing newline" (rendered.EndsWith("\n"))
    let reparsed = ok (parseNdjson "f" rendered)
    check "renderNdjson round-trip 2 entries" (reparsed.entries.Length = 2)
    equal "renderNdjson round-trip entry0 id" "0a3f" (idValue reparsed.entries.[0].id)
    equal "renderNdjson round-trip entry1 id" "b912" (idValue reparsed.entries.[1].id)

let projectionSpec () =
    let dh = DayHeader("2026-06-18", false)
    let oldEntry = entry "0a3f" ["e"] "old fact"
    let newEntry = entry "0a3f" ["e"] "new fact"
    let files = [ file dh [ oldEntry ]; file dh [ newEntry ] ]
    let proj = projectLatestWins files
    let resolved = Map.find (some (tryParseId "0a3f")) proj
    check "projectLatestWins latest wins" (resolved.entity = ["e"] && resolved.fact = "new fact")

let jobMarkerSpec () =
    let appendCtx = { workspaceRoot = "/tmp/kg-root"; kind = AppendAfterWork }
    let rendered = renderJobMarker appendCtx
    check "renderJobMarker is front matter" (rendered.StartsWith("---\n"))
    check "renderJobMarker includes type field" (rendered.Contains("type: \"vibe_knowledge_graph_job\""))
    check "renderJobMarker append round-trips" (tryParseJobMarker rendered = Some appendCtx)

    let merged = prependJobMarker appendCtx (buildAppendPrompt "T1" "input" "output" Map.empty)
    check "prependJobMarker keeps front matter form" (merged.StartsWith("---\n"))
    check "prependJobMarker merges workspaceRoot into prompt front matter" (merged.Contains("workspaceRoot: \"/tmp/kg-root\""))
    check "prependJobMarker preserves existing knowledge graph prompt fields" (merged.Contains("existing_knowledge_graph: []"))
    check "prependJobMarker merged prompt still parses" (tryParseJobMarker merged = Some appendCtx)

let preludeSpec () =
    check "buildPreludeSection empty None" (buildPreludeSection Map.empty |> Option.isNone)
    let e1 = entry "0a3f" ["项目插件入口"] "src/Opencode/Plugin.fs"
    let e2 = entry "b912" ["Magic Todo backlog"] "completedWorkReport"
    let e3 = entry "c001" ["项目插件入口"] "another fact"
    let proj = projection [ e1; e2; e3 ]
    match buildPreludeSection proj with
    | None -> check "buildPreludeSection non-empty Some" false
    | Some section ->
        check "prelude is front matter" (section.StartsWith("---\n"))
        check "prelude has knowledge_graph field" (section.Contains("knowledge_graph:"))
        check "prelude lists deduplicated entity e1" (section.Contains("项目插件入口"))
        check "prelude lists deduplicated entity e2" (section.Contains("Magic Todo backlog"))
        check "prelude has fetch instruction" (section.Contains("Call knowledge_graph_fetch(entity)"))
        check "prelude does NOT contain id" (not (section.Contains "0a3f") && not (section.Contains "b912") && not (section.Contains "c001"))
        check "prelude does NOT contain e1 fact" (not (section.Contains("src/Opencode/Plugin.fs")))
        check "prelude does NOT contain e2 fact" (not (section.Contains("completedWorkReport")))
    let longEntity = String('x', 200)
    let longEntry = entry "7c01" [longEntity] "a"
    let longProj = projection [ longEntry ]
    match buildPreludeSection longProj with
    | None -> check "prelude truncation section built" false
    | Some longSection ->
        check "prelude truncation marks ellipsis" (longSection.Contains("..."))

let fetchAnswerSpec () =
    let e1 = entry "0a3f" ["项目插件入口"] "src/Opencode/Plugin.fs"
    let e2 = entry "b912" ["项目插件入口"] "build/src/Mux/Plugin.js"
    let proj = projection [ e1; e2 ]
    let result = ok (fetchAnswer proj "项目插件入口")
    check "fetchAnswer concatenates facts for entity" (result.Contains "src/Opencode/Plugin.fs" && result.Contains "build/src/Mux/Plugin.js")
    check "fetchAnswer entity no match Error" (isErr (fetchAnswer proj "missing entity"))

let draftValidationSpec () =
    check "validateDraft valid id Ok" (isOk (validateDraft { id = Some "0a3f"; entity = ["e"]; fact = "f" }))
    check "validateDraft bad id Error" (isErr (validateDraft { id = Some "BAD"; entity = ["e"]; fact = "f" }))
    check "validateDraft empty entity Error" (isErr (validateDraft { id = None; entity = []; fact = "f" }))
    check "validateDraft empty fact Error" (isErr (validateDraft { id = None; entity = ["e"]; fact = "" }))
    check "validateDraft no id Ok" (isOk (validateDraft { id = None; entity = ["e"]; fact = "f" }))

let applyDraftsSpec () =
    let counter = ref 0
    let allocator (_existingIds: Set<string>) : string =
        counter.Value <- counter.Value + 1
        sprintf "%04x" counter.Value
    let existing = entry "0a3f" ["old e"] "old fact"
    let proj = projection [ existing ]
    let drafts =
        [ { id = Some "0a3f"; entity = ["updated e"]; fact = "updated fact" }
          { id = Some "9999"; entity = ["ghost e"]; fact = "ghost fact" }
          { id = None; entity = ["fresh e"]; fact = "fresh fact" } ]
    let results = ok (applyDrafts allocator proj drafts)
    check "applyDrafts 3 results" (results.Length = 3)
    equal "applyDrafts existing id reused" "0a3f" (idValue results.[0].id)
    equal "applyDrafts existing id entity" ["updated e"] results.[0].entity
    equal "applyDrafts existing id fact" "updated fact" results.[0].fact
    equal "applyDrafts ghost id reassigned" "0001" (idValue results.[1].id)
    equal "applyDrafts ghost entity kept" ["ghost e"] results.[1].entity
    equal "applyDrafts fresh id assigned" "0002" (idValue results.[2].id)
    equal "applyDrafts fresh entity kept" ["fresh e"] results.[2].entity
    let empty = ok (applyDrafts allocator proj [])
    check "applyDrafts empty Ok" empty.IsEmpty

let allocateSpec () =
    let mutable i = 4
    let src () =
        i <- i + 1
        i
    let existing = Set.ofList [ sprintf "%04x" (5 % 65536) ]
    match allocateRandomHexId src existing with
    | Ok id -> equal "allocateRandomHexId skips existing" (sprintf "%04x" (6 % 65536)) id
    | Error _ -> check "allocateRandomHexId should find next free" false
    let always5 () = 5
    let existing5 = Set.ofList [ sprintf "%04x" (5 % 65536) ]
    check "allocateRandomHexId exhausted Error" (isErr (allocateRandomHexId always5 existing5))

let run () : JS.Promise<unit> =
    promise {
        idParseSpec ()
        headerParseSpec ()
        headerRenderSpec ()
        entryParseRenderSpec ()
        ndjsonParseSpec ()
        ndjsonRenderSpec ()
        projectionSpec ()
        jobMarkerSpec ()
        preludeSpec ()
        fetchAnswerSpec ()
        draftValidationSpec ()
        applyDraftsSpec ()
        allocateSpec ()
    }
