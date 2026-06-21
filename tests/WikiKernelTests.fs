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

let private snapshotFile (through: string) (entries: WikiEntry list) : WikiFile =
    { header = SnapshotHeader(Some through); entries = entries }

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

let entriesThroughCutoffSpec () =
    let files =
        [ snapshotFile "2026-06-07" [ entry "1111" "snap q" "snap a" ]
          dayFile "2026-06-10" false [ entry "2222" "day q" "day a" ]
          dayFile "2026-06-20" false [ entry "3333" "later q" "later a" ] ]
    let through = entriesThroughCutoff files "2026-06-14"
    check "entriesThroughCutoff includes snapshot entry" (through |> List.exists (fun e -> idValue e.id = "1111"))
    check "entriesThroughCutoff includes day <= cutoff" (through |> List.exists (fun e -> idValue e.id = "2222"))
    check "entriesThroughCutoff excludes day > cutoff" (not (through |> List.exists (fun e -> idValue e.id = "3333")))

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
    let files = [ dayFile "2026-06-19" false [ entry "0a3f" "target q" "target a" ] ]
    let prompt = buildDailyPrompt "2026-06-19" files Map.empty
    check "daily prompt names rewrite role" (prompt.Contains "rewriting one day")
    check "daily prompt has target day field" (prompt.Contains "target_day:")
    check "daily prompt includes target entry" (prompt.Contains "target q")
    check "daily prompt is yaml-frontmatter markdown" (prompt.StartsWith "---\n")

let weeklyPromptSpec () =
    let files =
        [ snapshotFile "2026-06-07" [ entry "1111" "snap q" "snap a" ]
          dayFile "2026-06-10" false [ entry "2222" "day q" "day a" ] ]
    let prompt = buildWeeklyPrompt "2026-06-14" files Map.empty
    check "weekly prompt names snapshot rewrite role" (prompt.Contains "rewriting the project wiki snapshot")
    check "weekly prompt has previous snapshot field" (prompt.Contains "previous_snapshot:")
    check "weekly prompt has day-through-cutoff field" (prompt.Contains "day_files_through_cutoff:")
    check "weekly prompt includes previous snapshot entry" (prompt.Contains "snap q")
    check "weekly prompt includes day-through-cutoff entry" (prompt.Contains "day q")

// ── WikiMaintenance (pure scheduling decisions) ─────────────────────────────

let parseDateSpec () =
    check "parseDate valid" (parseDate "2026-06-19" |> Option.isSome)
    check "parseDate bad month" (parseDate "2026-13-01" |> Option.isNone)
    check "parseDate bad shape" (parseDate "not-a-date" |> Option.isNone)
    check "parseDate empty" (parseDate "" |> Option.isNone)

let addOneDaySpec () =
    check "addOneDay rolls month boundary" (addOneDay "2026-06-30" = "2026-07-01")
    check "addOneDay normal" (addOneDay "2026-06-19" = "2026-06-20")
    check "addOneDay bad unchanged" (addOneDay "nope" = "nope")

let dateRangeSpec () =
    equal "dateRangeInclusive ascending" [ "2026-06-19"; "2026-06-20"; "2026-06-21" ] (dateRangeInclusive "2026-06-19" "2026-06-21")
    check "dateRangeInclusive reversed empty" (dateRangeInclusive "2026-06-21" "2026-06-19" |> List.isEmpty)
    check "dateRangeInclusive single" (dateRangeInclusive "2026-06-19" "2026-06-19" = [ "2026-06-19" ])

let lastSundaySpec () =
    // 2026-06-20 is a Saturday (DayOfWeek = 6) → Sunday on or before is 2026-06-14.
    equal "lastSundayOnOrBefore Saturday -> prior Sunday" "2026-06-14" (lastSundayOnOrBefore (DateTime(2026, 6, 20)))
    // A Sunday stays itself.
    let sunday = DateTime(2026, 6, 14)
    equal "lastSundayOnOrBefore Sunday -> itself" "2026-06-14" (lastSundayOnOrBefore sunday)

let dueMaintenanceDailySpec () =
    // Past unrewritten day → daily due.
    let files = [ dayFile "2026-06-10" false [ entry "0a3f" "q" "a" ] ]
    let dailyDue, weeklyDue = dueMaintenance files (DateTime(2026, 6, 20))
    equal "daily due past unrewritten" ["2026-06-10"] dailyDue
    check "daily due scenario no weekly" (weeklyDue |> Option.isNone)
    // Rewritten past day -> not daily due.
    let dailyDue2, _ = dueMaintenance [ dayFile "2026-06-10" true [] ] (DateTime(2026, 6, 20))
    check "daily not due when rewritten" (dailyDue2 |> List.isEmpty)
    // Today's own day is not "past" -> not due.
    let dailyDue3, _ = dueMaintenance [ dayFile "2026-06-20" false [] ] (DateTime(2026, 6, 20))
    check "daily not due for today" (dailyDue3 |> List.isEmpty)
    // Multiple past unrewritten days -> list all, ascending, skip rewritten.
    let multiFiles =
        [ dayFile "2026-06-12" false [ entry "0a3f" "q" "a" ]
          dayFile "2026-06-10" false [ entry "0a40" "q2" "a2" ]
          dayFile "2026-06-11" true [] ]
    let dailyDue4, _ = dueMaintenance multiFiles (DateTime(2026, 6, 20))
    equal "daily due lists all past unrewritten ascending" ["2026-06-10"; "2026-06-12"] dailyDue4

let dueMaintenanceWeeklySpec () =
    // Snapshot through 2026-06-07, now 2026-06-20 (last Sunday 2026-06-14).
    // Gap days 06-08..06-14 are absent → treated as rewritten → weekly due at 06-14.
    let files = [ snapshotFile "2026-06-07" [ entry "1111" "q" "a" ] ]
    let dailyDue, weeklyDue = dueMaintenance files (DateTime(2026, 6, 20))
    check "weekly scenario no daily" (dailyDue |> List.isEmpty)
    equal "weekly due when gap all rewritten" (Some "2026-06-14") weeklyDue
    // A gap day that exists but is unrewritten blocks the weekly rewrite.
    let blockingFiles =
        [ snapshotFile "2026-06-07" [ entry "1111" "q" "a" ]
          dayFile "2026-06-10" false [ entry "2222" "q" "a" ] ]
    let _, weeklyDue2 = dueMaintenance blockingFiles (DateTime(2026, 6, 20))
    check "weekly blocked by unrewritten gap day" (weeklyDue2 |> Option.isNone)
    // No snapshot and no day files → nothing due.
    let dailyDue3, weeklyDue3 = dueMaintenance [] (DateTime(2026, 6, 20))
    check "empty wiki no daily" (dailyDue3 |> List.isEmpty)
    check "empty wiki no weekly" (weeklyDue3 |> Option.isNone)

let run () : JS.Promise<unit> =
    promise {
        projectionTextSpec ()
        filesTextSpec ()
        entriesForDaySpec ()
        entriesThroughCutoffSpec ()
        appendPromptSpec ()
        dailyPromptSpec ()
        weeklyPromptSpec ()
        parseDateSpec ()
        addOneDaySpec ()
        dateRangeSpec ()
        lastSundaySpec ()
        dueMaintenanceDailySpec ()
        dueMaintenanceWeeklySpec ()
    }
