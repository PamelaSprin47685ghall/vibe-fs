module VibeFs.Kernel.WikiPrompts

open System
open VibeFs.Kernel.Wiki

/// Pure wiki prompt assembly. These are string builders over Kernel wiki
/// types (projection / files / entries); they were inlined in the Shell-side
/// `WikiRuntime` class but carry no IO or mutable state, so they belong in the
/// Kernel layer (REFACTOR.md §2 / D12).

let private projectionLines (projection: WikiProjection) : string list =
    projection |> Map.toList |> List.map (fun (_, entry) -> renderEntry entry)

let projectionText (projection: WikiProjection) : string =
    let lines = projectionLines projection
    if lines.IsEmpty then "(empty)" else String.concat "\n" lines

let filesText (entries: WikiEntry list) : string =
    let lines = entries |> List.map renderEntry
    if lines.IsEmpty then "(empty)" else String.concat "\n" lines

let entriesForDay (files: WikiFile list) (date: string) : WikiEntry list =
    files
    |> List.tryPick (fun file ->
        match file.header with
        | DayHeader(day, _) when day = date -> Some file.entries
        | _ -> None)
    |> Option.defaultValue []

let entriesThroughCutoff (files: WikiFile list) (throughDate: string) : WikiEntry list =
    files
    |> List.collect (fun file ->
        match file.header with
        | SnapshotHeader _ -> file.entries
        | DayHeader(day, _) when day <= throughDate -> file.entries
        | _ -> [])

let private snapshotEntries (files: WikiFile list) : WikiEntry list =
    files
    |> List.tryPick (fun file ->
        match file.header with
        | SnapshotHeader _ -> Some file.entries
        | _ -> None)
    |> Option.defaultValue []

let buildAppendPrompt (title: string) (workInput: string) (workOutput: string) (rwSummary: string) (projection: WikiProjection) : string =
    let rwSection =
        if String.IsNullOrWhiteSpace rwSummary then None
        else Some ("=== RW Tool Summary ===\n" + rwSummary.Trim())
    let coreSections = [
        "You are the project wiki bookkeeper."
        "Submit exactly one `submit_wiki` call. Reuse existing ids when facts update, omit ids for new durable facts, and submit `[]` if nothing durable should be recorded."
        "=== Existing Wiki ==="
        projectionText projection
        "=== Work Title ==="
        title
        "=== Work Input ==="
        workInput
        "=== Work Output ==="
        workOutput
    ]
    let withRw =
        match rwSection with
        | Some section -> coreSections @ [ section ]
        | None -> coreSections
    String.concat "\n\n" (withRw @ [
        "=== Output Rules ==="
        "Record only stable project knowledge. Do not record temporary errors, progress chatter, or command noise."
    ])

let buildDailyPrompt (date: string) (files: WikiFile list) (projection: WikiProjection) : string =
    String.concat "\n\n"
        [ "You are rewriting one day of the project wiki."
          "Submit exactly one `submit_wiki` call. Replace the target day with durable canonical entries. It is valid to submit `[]`."
          "=== Current Wiki ==="
          projectionText projection
          "=== Target Day ==="
          filesText (entriesForDay files date) ]

let buildWeeklyPrompt (throughDate: string) (files: WikiFile list) (projection: WikiProjection) : string =
    let snapshotText = filesText (snapshotEntries files)
    let dayEntries = filesText (entriesThroughCutoff files throughDate)
    String.concat "\n\n"
        [ "You are rewriting the project wiki snapshot."
          "Submit exactly one `submit_wiki` call. Preserve surviving ids when possible, merge duplicates, omit ids only for genuinely new facts, and submit `[]` if nothing durable remains."
          "=== Current Wiki ==="
          projectionText projection
          "=== Previous Snapshot ==="
          snapshotText
          "=== Day Files Through Cutoff ==="
          dayEntries ]
