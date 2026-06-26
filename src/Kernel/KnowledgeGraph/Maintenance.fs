module VibeFs.Kernel.KnowledgeGraph.Maintenance

open System
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Kernel.KnowledgeGraph.RuntimeState

/// Pure knowledge graph maintenance scheduling. Given the current knowledge graph files and a clock
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

/// Classify the day-files present in the knowledge graph as `(date, rewritten)` pairs,
/// sorted ascending by date.
let dayFileSummary (files: KnowledgeGraphFile list) : (string * bool) list =
    files
    |> List.choose (fun file ->
        match file.header with
        | DayHeader(date, rewritten) -> Some(date, rewritten)
        )
    |> List.sortBy fst

/// The maintenance decisions, in one pure pass.
/// `dailyDue` = the oldest past day-file that has not yet been rewritten.
let dueMaintenance (files: KnowledgeGraphFile list) (now: DateTime) : string list =
    let todayStr = now.ToString("yyyy-MM-dd")
    dayFileSummary files
        |> List.filter (fun (date, rewritten) -> date < todayStr && not rewritten)
        |> List.map fst
        |> List.truncate 1

let private dailyMaintenanceTitle = "Daily knowledge graph rewrite"

/// Pure launch record + dedup key for one due daily rewrite (no IO).
let bookkeeperMaintenanceLaunch (workspaceRoot: string) (date: string) : string * BookkeeperLaunch =
    let resultPrefix = "daily"
    let promptInfix = "for"
    let key = workspaceRoot + "|" + resultPrefix + "|" + date
    let launch =
        { agent = "bookkeeper"
          title = dailyMaintenanceTitle
          prompt = $"{resultPrefix} maintenance due {promptInfix} {date}"
          result = $"{resultPrefix}:{date}" }
    key, launch
