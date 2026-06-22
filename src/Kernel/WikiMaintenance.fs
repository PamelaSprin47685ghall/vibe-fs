module VibeFs.Kernel.WikiMaintenance

open System
open VibeFs.Kernel.Wiki

/// Pure wiki maintenance scheduling. Given the current wiki files and a clock
/// value, decide which background daily rewrite jobs are due — without touching
/// disk or mutable state.

let parseDate (s: string) : DateTime option =
    match s.Split('-') with
    | [| yearStr; monthStr; dayStr |] ->
        match Int32.TryParse yearStr, Int32.TryParse monthStr, Int32.TryParse dayStr with
        | (true, year), (true, month), (true, day) when year >= 1 && year <= 9999 && month >= 1 && month <= 12 && day >= 1 && day <= 31 ->
            try Some (DateTime(year, month, day)) with _ -> None
        | _ -> None
    | _ -> None

/// Classify the day-files present in the wiki as `(date, rewritten)` pairs,
/// sorted ascending by date.
let dayFileSummary (files: WikiFile list) : (string * bool) list =
    files
    |> List.choose (fun file ->
        match file.header with
        | DayHeader(date, rewritten) -> Some(date, rewritten)
        )
    |> List.sortBy fst

/// The maintenance decisions, in one pure pass.
/// `dailyDue` = the oldest past day-file that has not yet been rewritten.
let dueMaintenance (files: WikiFile list) (now: DateTime) : string list =
    let todayStr = now.ToString("yyyy-MM-dd")
    dayFileSummary files
        |> List.filter (fun (date, rewritten) -> date < todayStr && not rewritten)
        |> List.map fst
        |> List.truncate 1
