module VibeFs.Kernel.WikiMaintenance

open System
open VibeFs.Kernel.Wiki

/// Pure wiki maintenance scheduling. Given the current wiki files and a clock
/// value, decide which background rewrite jobs are due — without touching disk
/// or mutable state. Lifted verbatim from the Shell `WikiRuntime` orchestrator
/// so the "when is a daily/weekly rewrite due?" rules live once, in the Kernel
/// (REFACTOR.md §2 / D12).

let parseDate (s: string) : DateTime option =
    match s.Split('-') with
    | [| yearStr; monthStr; dayStr |] ->
        match Int32.TryParse yearStr, Int32.TryParse monthStr, Int32.TryParse dayStr with
        | (true, year), (true, month), (true, day) when year >= 1 && year <= 9999 && month >= 1 && month <= 12 && day >= 1 && day <= 31 ->
            try Some (DateTime(year, month, day)) with _ -> None
        | _ -> None
    | _ -> None

let addOneDay (date: string) : string =
    match parseDate date with
    | Some d -> d.AddDays(1.0).ToString("yyyy-MM-dd")
    | None -> date

let dateRangeInclusive (start: string) (endDate: string) : string list =
    match parseDate start, parseDate endDate with
    | Some startDate, Some stopDate when startDate <= stopDate ->
        let rec loop (current: DateTime) (acc: string list) =
            if current > stopDate then List.rev acc
            else loop (current.AddDays(1.0)) (current.ToString("yyyy-MM-dd") :: acc)
        loop startDate []
    | _ -> []

/// The most recent Sunday on or before the given instant (matches the original
/// `lastSunday` definition: `now.AddDays(-int now.DayOfWeek)`).
let lastSundayOnOrBefore (now: DateTime) : string =
    now.AddDays(-int now.DayOfWeek).ToString("yyyy-MM-dd")

/// Classify the day-files present in the wiki as `(date, rewritten)` pairs,
/// sorted ascending by date.
let dayFileSummary (files: WikiFile list) : (string * bool) list =
    files
    |> List.choose (fun file ->
        match file.header with
        | DayHeader(date, rewritten) -> Some(date, rewritten)
        | _ -> None)
    |> List.sortBy fst

/// Resolve the effective snapshot cutoff: the snapshot file's `through` value
/// when present, otherwise the day before the oldest day-file (the implicit
/// starting frontier).
let snapshotThroughOf (files: WikiFile list) : string option =
    files
    |> List.tryPick (fun file ->
        match file.header with
        | SnapshotHeader through -> through
        | _ -> None)
    |> Option.orElseWith (fun () ->
        dayFileSummary files
        |> List.tryHead
        |> Option.bind (fun (oldestDate, _) -> parseDate oldestDate)
        |> Option.map (fun d -> d.AddDays(-1.0).ToString("yyyy-MM-dd")))

/// The maintenance decisions, in one pure pass: `(dailyDue, weeklyDue)`.
/// `dailyDue` = the oldest past day-file that has not yet been rewritten.
/// `weeklyDue` = the Sunday cutoff when a snapshot rewrite is due AND every day
/// between the current frontier and that Sunday has already been rewritten.
/// Both are empty/None when nothing is due. Mirrors the original inline logic.
let dueMaintenance (files: WikiFile list) (now: DateTime) : string list * string option =
    let todayStr = now.ToString("yyyy-MM-dd")
    let dayFiles = dayFileSummary files
    let dailyDue =
        dayFiles
        |> List.filter (fun (date, rewritten) -> date < todayStr && not rewritten)
        |> List.map fst
        |> List.truncate 1
    let weeklyDue =
        match snapshotThroughOf files with
        | None -> None
        | Some through ->
            let cutoff = lastSundayOnOrBefore now
            if cutoff <= through then None
            else
                let requiredDays = dateRangeInclusive (addOneDay through) cutoff
                let dayRewritten (date: string) : bool =
                    match dayFiles |> List.tryFind (fun (d, _) -> d = date) with
                    | Some(_, rewritten) -> rewritten
                    | None -> true
                if not requiredDays.IsEmpty && List.forall dayRewritten requiredDays then Some cutoff
                else None
    dailyDue, weeklyDue
