module VibeFs.Kernel.WikiPrompts

open System
open VibeFs.Kernel.Wiki
open VibeFs.Kernel.PromptFrontMatter

/// Pure wiki prompt assembly over Kernel wiki types. Front-matter helpers live
/// in the shared Kernel.PromptFrontMatter module; only wiki-specific entry
/// rendering stays here.

let private entrySeqLines (entries: WikiEntry list) : string list =
    entries
    |> List.map (fun entry ->
        String.concat "\n" [
            "  - id: " + idValue entry.id
            "    q: " + yamlScalar entry.q
            "    a: " + yamlScalar entry.a ])

let yamlSeqField (key: string) (entries: WikiEntry list) : string =
    PromptFrontMatter.yamlSeqField key (entrySeqLines entries)

let projectionText (projection: WikiProjection) : string =
    yamlSeqField "wiki" (projection |> Map.toList |> List.map snd)

let filesText (entries: WikiEntry list) : string =
    yamlSeqField "entries" entries

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

let private bookkeeperQualityRules = [
    "Write every recorded Q&A in modern compressed Chinese: drop filler, keep concepts and exact identifiers."
    "Record only stable project knowledge. Do not record temporary errors, progress chatter, command noise, or low-value test details (test names, fixture bodies, assertion counts) unless they encode a durable invariant."
]

/// Daily/weekly rewrites also prune the existing wiki: judge each entry by
/// whether a future reader will act on it, and keep the wiki short and
/// high-signal rather than exhaustive.
let private rewritePruneRules = [
    "Actively filter out low-value content already in the wiki: drop trivia no future reader will act on, remove transient details that have since stopped mattering, and merge overlapping facts into single canonical entries. Use judgment — prefer a shorter, higher-signal wiki over exhaustive preservation."
]

let buildAppendPrompt (title: string) (workInput: string) (workOutput: string) (projection: WikiProjection) : string =
    frontMatterPrompt [
        yamlSeqField "existing_wiki" (projection |> Map.toList |> List.map snd)
        yamlScalarField "work_title" title
        yamlBlockField "work_input" workInput
        yamlBlockField "work_output" workOutput
    ] (String.concat "\n\n" (
        [ "You are the project wiki bookkeeper."
          "Submit exactly one `return_bookkeeper` call. Reuse existing ids when facts update, omit ids for new durable facts, and return `[]` if nothing durable should be recorded." ]
        @ bookkeeperQualityRules))

let buildDailyPrompt (date: string) (files: WikiFile list) (projection: WikiProjection) : string =
    frontMatterPrompt [
        yamlSeqField "current_wiki" (projection |> Map.toList |> List.map snd)
        yamlSeqField "target_day" (entriesForDay files date)
    ] (String.concat "\n\n" (
        [ "You are rewriting one day of the project wiki."
          "Submit exactly one `return_bookkeeper` call. Replace the target day with durable canonical entries. It is valid to return `[]`." ]
        @ bookkeeperQualityRules @ rewritePruneRules))

let buildWeeklyPrompt (throughDate: string) (files: WikiFile list) (projection: WikiProjection) : string =
    frontMatterPrompt [
        yamlSeqField "current_wiki" (projection |> Map.toList |> List.map snd)
        yamlSeqField "previous_snapshot" (snapshotEntries files)
        yamlSeqField "day_files_through_cutoff" (entriesThroughCutoff files throughDate)
    ] (String.concat "\n\n" (
        [ "You are rewriting the project wiki snapshot."
          "Submit exactly one `return_bookkeeper` call. Preserve surviving ids when possible, merge duplicates, omit ids only for genuinely new facts, and return `[]` if nothing durable remains." ]
        @ bookkeeperQualityRules @ rewritePruneRules))
