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

let private eventSeqLines (entries: WikiEntry list) : string list =
    entries
    |> List.map (fun entry ->
        String.concat "\n" [
            "  - q: " + yamlScalar entry.q
            "    a: " + yamlScalar entry.a ])

let yamlSeqField (key: string) (entries: WikiEntry list) : string =
    PromptFrontMatter.yamlSeqField key (entrySeqLines entries)

let private yamlEventSeqField (key: string) (entries: WikiEntry list) : string =
    PromptFrontMatter.yamlSeqField key (eventSeqLines entries)

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

let private filesBefore (date: string) (files: WikiFile list) : WikiFile list =
    files
    |> List.filter (fun file ->
        match file.header with
        | SnapshotHeader _ -> true
        | DayHeader(day, _) -> day < date)

let private entriesProjection (entries: WikiEntry list) : WikiProjection =
    entries |> List.fold (fun acc entry -> Map.add entry.id entry acc) Map.empty

let private projectionEntries (projection: WikiProjection) : WikiEntry list =
    projection |> Map.toList |> List.map snd

let private projectionDelta (before: WikiProjection) (after: WikiProjection) : WikiEntry list =
    after
    |> Map.toList
    |> List.choose (fun (id, entry) ->
        match Map.tryFind id before with
        | Some oldEntry when oldEntry.q = entry.q && oldEntry.a = entry.a -> None
        | _ -> Some entry)

let private foldedEntriesBefore (date: string) (files: WikiFile list) : WikiEntry list =
    files
    |> filesBefore date
    |> projectLatestWins
    |> projectionEntries

let private deltaEntriesForDay (date: string) (files: WikiFile list) : WikiEntry list =
    projectionDelta (files |> filesBefore date |> projectLatestWins) (entriesForDay files date |> entriesProjection)

let private snapshotThrough (files: WikiFile list) : string option =
    files
    |> List.tryPick (fun file ->
        match file.header with
        | SnapshotHeader through -> through
        | _ -> None)

let private eventsThroughCutoff (files: WikiFile list) (throughDate: string) : WikiEntry list =
    let lowerBound = snapshotThrough files
    files
    |> List.collect (fun file ->
        match file.header with
        | DayHeader(day, _) when day <= throughDate && (lowerBound |> Option.forall (fun previous -> day > previous)) -> file.entries
        | _ -> [])

let private deltaEntriesThroughCutoff (files: WikiFile list) (throughDate: string) : WikiEntry list =
    let existing = snapshotEntries files
    projectionDelta (entriesProjection existing) (eventsThroughCutoff files throughDate |> entriesProjection)

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

let buildDailyPrompt (date: string) (files: WikiFile list) (_projection: WikiProjection) : string =
    frontMatterPrompt [
        yamlSeqField "existing_wiki" (foldedEntriesBefore date files)
        yamlEventSeqField "new_events" (deltaEntriesForDay date files)
    ] (String.concat "\n\n" (
        [ "You are the project wiki bookkeeper."
          "You are given existing wiki entries. Some new events happened. Organize and merge the new events, then modify the existing wiki entries."
          "Submit exactly one `return_bookkeeper` call. Reuse existing ids when facts update, omit ids for new durable facts, and return `[]` if nothing durable should be changed." ]
        @ bookkeeperQualityRules @ rewritePruneRules))

let buildWeeklyPrompt (throughDate: string) (files: WikiFile list) (_projection: WikiProjection) : string =
    frontMatterPrompt [
        yamlSeqField "existing_wiki" (snapshotEntries files)
        yamlEventSeqField "new_events" (deltaEntriesThroughCutoff files throughDate)
    ] (String.concat "\n\n" (
        [ "You are the project wiki bookkeeper."
          "You are given existing wiki entries. Some new events happened. Organize and merge the new events, then modify the existing wiki entries."
          "Submit exactly one `return_bookkeeper` call. Reuse existing ids when facts update, omit ids for new durable facts, and return `[]` if nothing durable should be changed." ]
        @ bookkeeperQualityRules @ rewritePruneRules))
