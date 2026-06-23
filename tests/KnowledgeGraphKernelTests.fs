module VibeFs.Tests.KnowledgeGraphKernelTests

open System
open Fable.Core
open VibeFs.Tests.Assert
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraphPrompts
open VibeFs.Kernel.KnowledgeGraphMaintenance

let private some (o: 'a option) : 'a =
    match o with Some v -> v | None -> failwith "expected Some"

let private entry (idStr: string) (entities: string list) (fact: string) : KnowledgeGraphEntry =
    { id = some (tryParseId idStr); entity = entities; fact = fact }

let private dayFile (date: string) (rewritten: bool) (entries: KnowledgeGraphEntry list) : KnowledgeGraphFile =
    { header = DayHeader(date, rewritten); entries = entries }

let private projectionOf (entries: KnowledgeGraphEntry list) : KnowledgeGraphProjection =
    entries |> List.map (fun e -> e.id, e) |> Map.ofList

// ── KnowledgeGraphPrompts (pure prompt assembly) ──────────────────────────────────────

let projectionTextSpec () =
    check "projectionText empty renders empty yaml seq" (projectionText Map.empty = "knowledge_graph: []")
    let proj = projectionOf [ entry "0a3f" ["e1"] "f1" ]
    let text = projectionText proj
    check "projectionText non-empty renders entity" (text.Contains "e1")
    check "projectionText non-empty renders fact" (text.Contains "f1")

let filesTextSpec () =
    check "filesText empty renders empty yaml seq" (filesText [] = "entries: []")
    let text = filesText [ entry "0a3f" ["e"] "f" ]
    check "filesText renders entry entity" (text.Contains "e")
    check "filesText renders entry fact" (text.Contains "f")

let entriesForDaySpec () =
    let files = [ dayFile "2026-06-19" false [ entry "0a3f" ["e"] "f" ] ]
    check "entriesForDay matches day" ((entriesForDay files "2026-06-19").Length = 1)
    check "entriesForDay misses other day" ((entriesForDay files "2026-06-20").IsEmpty)

let appendPromptSpec () =
    let proj = projectionOf [ entry "0a3f" ["existing e"] "existing f" ]
    let prompt = buildAppendPrompt "T1" "do work" "got result" proj
    check "append prompt names KnowledgeGraph bookkeeper role" (prompt.Contains "KnowledgeGraph bookkeeper")
    check "append prompt embeds title" (prompt.Contains "T1")
    check "append prompt embeds work input" (prompt.Contains "do work")
    check "append prompt embeds work output" (prompt.Contains "got result")
    check "append prompt embeds existing knowledge graph" (prompt.Contains "existing e")
    check "append prompt is yaml-frontmatter markdown" (prompt.StartsWith("---\n"))
    check "append prompt has existing_knowledge_graph field" (prompt.Contains "existing_knowledge_graph:")

let dailyPromptSpec () =
    let files =
        [ dayFile "2026-06-18" true [ entry "1111" ["history e"] "history f" ]
          dayFile "2026-06-19" false [ entry "2222" ["stale target e"] "stale target f"; entry "2222" ["target e"] "target f" ]
          dayFile "2026-06-20" false [ entry "3333" ["future e"] "future f" ] ]
    let prompt = buildDailyPrompt "2026-06-19" files Map.empty
    check "daily prompt uses KnowledgeGraph bookkeeper role" (prompt.Contains "project KnowledgeGraph bookkeeper")
    check "daily prompt has existing knowledge graph field" (prompt.Contains "existing_knowledge_graph:")
    check "daily prompt has new facts field" (prompt.Contains "new_facts:")
    check "daily prompt includes previous folded knowledge graph" (prompt.Contains "history e")
    check "daily prompt includes target event" (prompt.Contains "target e")
    check "daily prompt folds target events last-win" (not (prompt.Contains "stale target e"))
    check "daily prompt excludes future events" (not (prompt.Contains "future e"))
    check "daily prompt hides target event id" (not (prompt.Contains "id: 2222"))
    check "daily prompt keeps existing knowledge graph ids" (prompt.Contains "id: 1111")
    check "daily prompt does not use old target day payload field" (not (prompt.Contains "target_day:\n"))
    check "daily prompt hides implementation vocabulary" (not (prompt.Contains "last-win") && not (prompt.Contains "delta") && not (prompt.Contains "rewritten"))
    check "daily prompt uses simple maintenance instruction" (prompt.Contains "Some new events happened" && prompt.Contains "modify the existing knowledge graph entries")
    check "daily prompt is yaml-frontmatter markdown" (prompt.StartsWith("---\n"))
    let rollbackPrompt =
        buildDailyPrompt "2026-06-19"
            [ dayFile "2026-06-18" true [ entry "2222" ["old e"] "old f" ]
              dayFile "2026-06-19" false [ entry "2222" ["changed e"] "changed f"; entry "2222" ["old e"] "old f" ] ]
            Map.empty
    check "daily prompt drops events that roll back to old value" (not (rollbackPrompt.Contains "changed e") && not (rollbackPrompt.Contains "new_facts:\n  -"))

// ── KnowledgeGraphMaintenance (pure scheduling decisions) ─────────────────────────────

let parseDateSpec () =
    check "parseDate valid" (parseDate "2026-06-19" |> Option.isSome)
    check "parseDate bad month" (parseDate "2026-13-01" |> Option.isNone)
    check "parseDate bad shape" (parseDate "not-a-date" |> Option.isNone)
    check "parseDate empty" (parseDate "" |> Option.isNone)

let dueMaintenanceDailySpec () =
    // Past unrewritten day → daily due.
    let files = [ dayFile "2026-06-10" false [ entry "0a3f" ["e"] "f" ] ]
    let dailyDue = dueMaintenance files (DateTime(2026, 6, 20))
    equal "daily due past unrewritten" ["2026-06-10"] dailyDue
    // Rewritten past day -> not daily due.
    let dailyDue2 = dueMaintenance [ dayFile "2026-06-10" true [] ] (DateTime(2026, 6, 20))
    check "daily not due when rewritten" (dailyDue2 |> List.isEmpty)
    // Today's own day is not "past" -> not due.
    let dailyDue3 = dueMaintenance [ dayFile "2026-06-20" false [] ] (DateTime(2026, 6, 20))
    check "daily not due for today" (dailyDue3 |> List.isEmpty)
    // Multiple past unrewritten days -> only the oldest is due; later days wait
    // until the earlier rewrite is persisted.
    let multiFiles =
        [ dayFile "2026-06-12" false [ entry "0a3f" ["e"] "f" ]
          dayFile "2026-06-10" false [ entry "0a40" ["e2"] "f2" ]
          dayFile "2026-06-11" true [] ]
    let dailyDue4 = dueMaintenance multiFiles (DateTime(2026, 6, 20))
    equal "daily due schedules oldest past unrewritten only" ["2026-06-10"] dailyDue4

let run () : JS.Promise<unit> =
    promise {
        projectionTextSpec ()
        filesTextSpec ()
        entriesForDaySpec ()
        appendPromptSpec ()
        dailyPromptSpec ()
        parseDateSpec ()
        dueMaintenanceDailySpec ()
    }
