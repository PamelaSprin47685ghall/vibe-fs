module VibeFs.Kernel.KnowledgeGraph.Prompts

open System
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Kernel.PromptFrontMatter
open VibeFs.Kernel.Yaml

let private entryItem (entry: KnowledgeGraphEntry) : obj =
    createObj [ "id", box (idValue entry.id); "entity", box (List.toArray entry.entity); "fact", box entry.fact ]

let private eventItem (entry: KnowledgeGraphEntry) : obj =
    createObj [ "entity", box (List.toArray entry.entity); "fact", box entry.fact ]

let kgYamlSeqField (key: string) (entries: KnowledgeGraphEntry list) : FrontMatterField =
    yamlSeqField key (entries |> List.map entryItem)

let private kgYamlEventSeqField (key: string) (entries: KnowledgeGraphEntry list) : FrontMatterField =
    yamlSeqField key (entries |> List.map eventItem)

let projectionText (projection: KnowledgeGraphProjection) : string =
    stringifyFields [ kgYamlSeqField "knowledge_graph" (projection |> Map.toList |> List.map snd) ]

let filesText (entries: KnowledgeGraphEntry list) : string =
    stringifyFields [ kgYamlSeqField "entries" entries ]

let entriesForDay (files: KnowledgeGraphFile list) (date: string) : KnowledgeGraphEntry list =
    files
    |> List.tryPick (fun file ->
        match file.header with
        | DayHeader(day, _) when day = date -> Some file.entries
        | _ -> None)
    |> Option.defaultValue []

let private filesBefore (date: string) (files: KnowledgeGraphFile list) : KnowledgeGraphFile list =
    files
    |> List.filter (fun file ->
        match file.header with
        | DayHeader(day, _) -> day < date)

let private entriesProjection (entries: KnowledgeGraphEntry list) : KnowledgeGraphProjection =
    entries |> List.fold (fun acc entry -> Map.add entry.id entry acc) Map.empty

let private projectionEntries (projection: KnowledgeGraphProjection) : KnowledgeGraphEntry list =
    projection |> Map.toList |> List.map snd

let private projectionDelta (before: KnowledgeGraphProjection) (after: KnowledgeGraphProjection) : KnowledgeGraphEntry list =
    after
    |> Map.toList
    |> List.choose (fun (id, entry) ->
        match Map.tryFind id before with
        | Some oldEntry when oldEntry.entity = entry.entity && oldEntry.fact = entry.fact -> None
        | _ -> Some entry)

let private foldedEntriesBefore (date: string) (files: KnowledgeGraphFile list) : KnowledgeGraphEntry list =
    files
    |> filesBefore date
    |> projectLatestWins
    |> projectionEntries

let private deltaEntriesForDay (date: string) (files: KnowledgeGraphFile list) : KnowledgeGraphEntry list =
    projectionDelta (files |> filesBefore date |> projectLatestWins) (entriesForDay files date |> entriesProjection)

let private bookkeeperQualityRules = [
    "Write every recorded fact in modern compressed Chinese: drop filler, keep concepts and exact identifiers."
    "Record only stable project knowledge. Do not record temporary errors, progress chatter, command noise, or low-value test details (test names, fixture bodies, assertion counts) unless they encode a durable invariant."
    "Every `entity` must be a Chinese abstract knowledge concept. Reuse existing entities if possible; do not invent synonymous tags."
    "Entities must NEVER be file names, module names, function names, variable names, class names, tool names, agent names, host names, language names, framework names, or any other accidental implementation label."
    "Keep the entity vocabulary small and strong, preferably using domain-specific terms."
]

let private rewritePruneRules = [
    "Actively filter out low-value content already in the knowledge graph: drop trivia no future reader will act on, remove transient details that have since stopped mattering, and merge overlapping facts into single canonical entries. Use judgment — prefer a shorter, higher-signal knowledge graph over exhaustive preservation."
]

let buildAppendPrompt (title: string) (workInput: string) (workOutput: string) (projection: KnowledgeGraphProjection) : string =
    frontMatterPrompt [
        kgYamlSeqField "existing_knowledge_graph" (projection |> Map.toList |> List.map snd)
        yamlField "work_title" title
        yamlField "work_input" workInput
        yamlField "work_output" workOutput
    ] (String.concat "\n\n" (
        [ "You are the project KnowledgeGraph bookkeeper."
          "Submit exactly one `return_bookkeeper` call. Reuse existing ids when facts update, omit ids for new durable facts, and return `[]` if nothing durable should be recorded." ]
        @ bookkeeperQualityRules))

let private existingEntriesForDaily (date: string) (files: KnowledgeGraphFile list) : KnowledgeGraphEntry list =
    let before = files |> filesBefore date |> projectLatestWins
    let day = entriesForDay files date |> entriesProjection
    before
    |> Map.toList
    |> List.map (fun (id, oldEntry) ->
        match Map.tryFind id day with
        | Some updated -> updated
        | None -> oldEntry)
    |> List.sortBy (fun e -> idValue e.id)

let buildDailyPrompt (date: string) (files: KnowledgeGraphFile list) (_projection: KnowledgeGraphProjection) : string =
    frontMatterPrompt [
        kgYamlSeqField "existing_knowledge_graph" (existingEntriesForDaily date files)
        kgYamlEventSeqField "new_facts" (deltaEntriesForDay date files)
    ] (String.concat "\n\n" (
        [ "You are the project KnowledgeGraph bookkeeper."
          "You are given existing knowledge graph entries. Some new events happened. Organize and merge the new facts, then modify the existing knowledge graph entries."
          "Submit exactly one `return_bookkeeper` call. Reuse existing ids when facts update, omit ids for new durable facts, and return `[]` if nothing durable should be changed." ]
        @ bookkeeperQualityRules @ rewritePruneRules))
