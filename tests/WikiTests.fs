module VibeFs.Tests.WikiTests

open System
open Fable.Core
open VibeFs.Tests.Assert
open VibeFs.Kernel.Wiki

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

let private entry idStr q a : WikiEntry =
    { id = some (tryParseId idStr); q = q; a = a }

let private file (header: WikiHeader) (entries: WikiEntry list) : WikiFile = { header = header; entries = entries }

let private projection (entries: WikiEntry list) : WikiProjection =
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
    let dayLine = """{"type":"wiki_header","version":1,"kind":"day","date":"2026-06-19","rewritten":false}"""
    let snapLine = """{"type":"wiki_header","version":1,"kind":"snapshot","through":"2026-06-14"}"""
    check "parseHeaderLine day Ok DayHeader" (match parseHeaderLine dayLine with Ok(DayHeader("2026-06-19", false)) -> true | _ -> false)
    check "parseHeaderLine snapshot Ok SnapshotHeader" (match parseHeaderLine snapLine with Ok(SnapshotHeader(Some "2026-06-14")) -> true | _ -> false)
    check "parseHeaderLine garbage Error" (isErr (parseHeaderLine "not json at all"))
    let snapNoThrough = """{"type":"wiki_header","version":1,"kind":"snapshot"}"""
    check "parseHeaderLine snapshot missing through Error" (isErr (parseHeaderLine snapNoThrough))

let headerRenderSpec () =
    let dayRendered = renderHeader (DayHeader("2026-06-18", true))
    check "renderHeader day contains rewritten true" (dayRendered.Contains("\"rewritten\":true"))
    check "renderHeader day contains kind day" (dayRendered.Contains("\"kind\":\"day\""))
    check "renderHeader day contains date" (dayRendered.Contains("2026-06-18"))
    let snapRendered = renderHeader (SnapshotHeader(Some "2026-06-14"))
    check "renderHeader snapshot contains kind snapshot" (snapRendered.Contains("\"kind\":\"snapshot\""))
    check "renderHeader snapshot contains through" (snapRendered.Contains("\"through\":\"2026-06-14\""))
    let dh = DayHeader("2026-06-18", true)
    check "header round-trip day" (parseHeaderLine (renderHeader dh) = Ok dh)
    let sh = SnapshotHeader(Some "2026-06-14")
    check "header round-trip snapshot" (parseHeaderLine (renderHeader sh) = Ok sh)

let entryParseRenderSpec () =
    let line = """{"id":"0a3f","q":"项目插件入口在哪里？","a":"src/Opencode/Plugin.fs"}"""
    let parsed = ok (parseEntryLine line)
    equal "parseEntry id" "0a3f" (idValue parsed.id)
    equal "parseEntry q" "项目插件入口在哪里？" parsed.q
    equal "parseEntry a" "src/Opencode/Plugin.fs" parsed.a
    let rerendered = renderEntry parsed
    let reparsed = ok (parseEntryLine rerendered)
    equal "entry render round-trip id" (idValue parsed.id) (idValue reparsed.id)
    equal "entry render round-trip q" parsed.q reparsed.q
    equal "entry render round-trip a" parsed.a reparsed.a
    let missingA = """{"id":"0a3f","q":"q"}"""
    check "parseEntry missing a Error" (isErr (parseEntryLine missingA))
    let badId5 = """{"id":"0a3f5","q":"q","a":"a"}"""
    check "parseEntry bad id 5 chars Error" (isErr (parseEntryLine badId5))
    let badIdUpper = """{"id":"FFFF","q":"q","a":"a"}"""
    check "parseEntry bad id uppercase Error" (isErr (parseEntryLine badIdUpper))

let ndjsonParseSpec () =
    let dayHeader = """{"type":"wiki_header","version":1,"kind":"day","date":"2026-06-19","rewritten":false}"""
    let e1 = """{"id":"0a3f","q":"q1","a":"a1"}"""
    let e2 = """{"id":"b912","q":"q2","a":"a2"}"""
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
    let e1 = entry "0a3f" "q1" "a1"
    let e2 = entry "b912" "q2" "a2"
    let rendered = renderNdjson dh [ e1; e2 ]
    check "renderNdjson has trailing newline" (rendered.EndsWith("\n"))
    let reparsed = ok (parseNdjson "f" rendered)
    check "renderNdjson round-trip 2 entries" (reparsed.entries.Length = 2)
    equal "renderNdjson round-trip entry0 id" "0a3f" (idValue reparsed.entries.[0].id)
    equal "renderNdjson round-trip entry1 id" "b912" (idValue reparsed.entries.[1].id)

let projectionSpec () =
    let dh = DayHeader("2026-06-18", false)
    let oldEntry = entry "0a3f" "q old" "a old"
    let newEntry = entry "0a3f" "q new" "a new"
    let files = [ file dh [ oldEntry ]; file dh [ newEntry ] ]
    let proj = projectLatestWins files
    let resolved = Map.find (some (tryParseId "0a3f")) proj
    check "projectLatestWins latest wins" (resolved.q = "q new" && resolved.a = "a new")

let preludeSpec () =
    check "buildPreludeSection empty None" (buildPreludeSection Map.empty |> Option.isNone)
    let e1 = entry "0a3f" "项目插件入口在哪里？" "src/Opencode/Plugin.fs"
    let e2 = entry "b912" "Magic Todo backlog 如何保存？" "completedWorkReport"
    let proj = projection [ e1; e2 ]
    match buildPreludeSection proj with
    | None -> check "buildPreludeSection non-empty Some" false
    | Some section ->
        check "prelude is front matter" (section.StartsWith("---\n"))
        check "prelude has wiki field" (section.Contains("wiki:"))
        check "prelude has id e1" (section.Contains("0a3f"))
        check "prelude has question e1" (section.Contains("项目插件入口在哪里？"))
        check "prelude has id e2" (section.Contains("b912"))
        check "prelude has fetch instruction" (section.Contains("Call fetch_wiki(id)"))
        check "prelude does NOT contain e1 answer" (not (section.Contains("src/Opencode/Plugin.fs")))
        check "prelude does NOT contain e2 answer" (not (section.Contains("completedWorkReport")))
    let longQ = String('x', 200)
    let longEntry = entry "7c01" longQ "a"
    let longProj = projection [ longEntry ]
    match buildPreludeSection longProj with
    | None -> check "prelude truncation section built" false
    | Some longSection ->
        check "prelude truncation marks ellipsis" (longSection.Contains("..."))

let draftValidationSpec () =
    check "validateDraft valid id Ok" (isOk (validateDraft { id = Some "0a3f"; q = "q"; a = "a" }))
    check "validateDraft bad id Error" (isErr (validateDraft { id = Some "BAD"; q = "q"; a = "a" }))
    check "validateDraft empty q Error" (isErr (validateDraft { id = None; q = ""; a = "a" }))
    check "validateDraft empty a Error" (isErr (validateDraft { id = None; q = "q"; a = "" }))
    check "validateDraft no id Ok" (isOk (validateDraft { id = None; q = "q"; a = "a" }))

let applyDraftsSpec () =
    let counter = ref 0
    let allocator (_existingIds: Set<string>) : string =
        counter.Value <- counter.Value + 1
        sprintf "%04x" counter.Value
    let existing = entry "0a3f" "old q" "old a"
    let proj = projection [ existing ]
    let drafts =
        [ { id = Some "0a3f"; q = "updated q"; a = "updated a" }
          { id = Some "9999"; q = "ghost q"; a = "ghost a" }
          { id = None; q = "fresh q"; a = "fresh a" } ]
    let results = ok (applyDrafts allocator proj drafts)
    check "applyDrafts 3 results" (results.Length = 3)
    equal "applyDrafts existing id reused" "0a3f" (idValue results.[0].id)
    equal "applyDrafts existing id q" "updated q" results.[0].q
    equal "applyDrafts existing id a" "updated a" results.[0].a
    equal "applyDrafts ghost id reassigned" "0001" (idValue results.[1].id)
    equal "applyDrafts ghost q kept" "ghost q" results.[1].q
    equal "applyDrafts fresh id assigned" "0002" (idValue results.[2].id)
    equal "applyDrafts fresh q kept" "fresh q" results.[2].q
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
        preludeSpec ()
        draftValidationSpec ()
        applyDraftsSpec ()
        allocateSpec ()
    }