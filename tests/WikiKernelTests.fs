module VibeFs.Tests.WikiKernelTests

open System
open Fable.Core
open VibeFs.Tests.Assert
open VibeFs.Kernel.Wiki
open VibeFs.Kernel.WikiPrompts
open VibeFs.Kernel.WikiMaintenance

let private some (o: 'a option) : 'a =
    match o with Some v -> v | None -> failwith "expected Some"

let private entry (idStr: string) (q: string) (a: string) : WikiEntry =
    { id = some (tryParseId idStr); q = q; a = a }

let private dayFile (date: string) (rewritten: bool) (entries: WikiEntry list) : WikiFile =
    { header = DayHeader(date, rewritten); entries = entries }

let private projectionOf (entries: WikiEntry list) : WikiProjection =
    entries |> List.map (fun e -> e.id, e) |> Map.ofList

// ── WikiPrompts (pure prompt assembly) ──────────────────────────────────────

let projectionTextSpec () =
    check "projectionText empty renders empty yaml seq" (projectionText Map.empty = "wiki: []")
    let proj = projectionOf [ entry "0a3f" "q1" "a1" ]
    let text = projectionText proj
    check "projectionText non-empty renders id" (text.Contains "0a3f")
    check "projectionText non-empty renders q" (text.Contains "q1")

let filesTextSpec () =
    check "filesText empty renders empty yaml seq" (filesText [] = "entries: []")
    let text = filesText [ entry "0a3f" "q" "a" ]
    check "filesText renders entry" (text.Contains "0a3f")

let entriesForDaySpec () =
    let files = [ dayFile "2026-06-19" false [ entry "0a3f" "q" "a" ] ]
    check "entriesForDay matches day" ((entriesForDay files "2026-06-19").Length = 1)
    check "entriesForDay misses other day" ((entriesForDay files "2026-06-20").IsEmpty)

let appendPromptSpec () =
    let proj = projectionOf [ entry "0a3f" "existing q" "existing a" ]
    let prompt = buildAppendPrompt "T1" "do work" "got result" proj
    check "append prompt names bookkeeper role" (prompt.Contains "wiki bookkeeper")
    check "append prompt embeds title" (prompt.Contains "T1")
    check "append prompt embeds work input" (prompt.Contains "do work")
    check "append prompt embeds work output" (prompt.Contains "got result")
    check "append prompt embeds existing wiki" (prompt.Contains "existing q")
    check "append prompt is yaml-frontmatter markdown" (prompt.StartsWith "---\n")
    check "append prompt has existing_wiki field" (prompt.Contains "existing_wiki:")

let dailyPromptSpec () =
    let files =
        [ dayFile "2026-06-18" true [ entry "1111" "history q" "history a" ]
          dayFile "2026-06-19" false [ entry "2222" "stale target q" "stale target a"; entry "2222" "target q" "target a" ]
          dayFile "2026-06-20" false [ entry "3333" "future q" "future a" ] ]
    let prompt = buildDailyPrompt "2026-06-19" files Map.empty
    check "daily prompt uses bookkeeper role" (prompt.Contains "project wiki bookkeeper")
    check "daily prompt has existing wiki field" (prompt.Contains "existing_wiki:")
    check "daily prompt has new events field" (prompt.Contains "new_events:")
    check "daily prompt includes previous folded wiki" (prompt.Contains "history q")
    check "daily prompt includes target event" (prompt.Contains "target q")
    check "daily prompt folds target events last-win" (not (prompt.Contains "stale target q"))
    check "daily prompt excludes future events" (not (prompt.Contains "future q"))
    check "daily prompt hides target event id" (not (prompt.Contains "id: 2222"))
    check "daily prompt keeps existing wiki ids" (prompt.Contains "id: 1111")
    check "daily prompt does not use old target day payload field" (not (prompt.Contains "target_day:\n"))
    check "daily prompt hides implementation vocabulary" (not (prompt.Contains "last-win") && not (prompt.Contains "delta") && not (prompt.Contains "rewritten"))
    check "daily prompt uses simple maintenance instruction" (prompt.Contains "Some new events happened" && prompt.Contains "modify the existing wiki entries")
    check "daily prompt is yaml-frontmatter markdown" (prompt.StartsWith "---\n")
    let rollbackPrompt =
        buildDailyPrompt "2026-06-19"
            [ dayFile "2026-06-18" true [ entry "2222" "old q" "old a" ]
              dayFile "2026-06-19" false [ entry "2222" "changed q" "changed a"; entry "2222" "old q" "old a" ] ]
            Map.empty
    check "daily prompt drops events that roll back to old value" (not (rollbackPrompt.Contains "changed q") && not (rollbackPrompt.Contains "new_events:\n  -"))

// ── WikiMaintenance (pure scheduling decisions) ─────────────────────────────

let parseDateSpec () =
    check "parseDate valid" (parseDate "2026-06-19" |> Option.isSome)
    check "parseDate bad month" (parseDate "2026-13-01" |> Option.isNone)
    check "parseDate bad shape" (parseDate "not-a-date" |> Option.isNone)
    check "parseDate empty" (parseDate "" |> Option.isNone)

let dueMaintenanceDailySpec () =
    // Past unrewritten day → daily due.
    let files = [ dayFile "2026-06-10" false [ entry "0a3f" "q" "a" ] ]
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
        [ dayFile "2026-06-12" false [ entry "0a3f" "q" "a" ]
          dayFile "2026-06-10" false [ entry "0a40" "q2" "a2" ]
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
