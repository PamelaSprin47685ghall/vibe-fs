module Wanxiangshu.Runtime.PromptFrontMatter

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Yaml
open Wanxiangshu.Runtime.PromptFrontMatterParse

type FrontMatterField = string * obj

let extractFrontMatterBlock = PromptFrontMatterParse.extractFrontMatterBlock
let bodyAfterFrontMatter = PromptFrontMatterParse.bodyAfterFrontMatter
let parseFrontMatter = PromptFrontMatterParse.parseFrontMatter
let parseFrontMatterBlocks = PromptFrontMatterParse.parseFrontMatterBlocks
let parseFrontMatterScalars = PromptFrontMatterParse.parseFrontMatterScalars

let parseFrontMatterScalarBlocks =
    PromptFrontMatterParse.parseFrontMatterScalarBlocks

let yamlField (key: string) (value: string) : FrontMatterField = (key, box value)

let yamlStringSeqField (key: string) (values: string list) : FrontMatterField = (key, box (values |> List.toArray))

let yamlSeqField (key: string) (items: obj list) : FrontMatterField = (key, box (items |> List.toArray))

let frontMatter (fields: FrontMatterField list) : string =
    if List.isEmpty fields then
        ""
    else
        let obj = createObj fields
        let body = Yaml.stringify obj
        "---\n" + body.TrimEnd('\n') + "\n---"

let frontMatterPrompt (fields: FrontMatterField list) (prose: string) : string =
    if List.isEmpty fields then
        prose
    else
        frontMatter fields + "\n\n" + prose

let frontMatterRoot (value: obj) : string =
    let body = Yaml.stringify value
    "---\n" + body.TrimEnd('\n') + "\n---"

let frontMatterPromptRoot (value: obj) (prose: string) : string = frontMatterRoot value + "\n\n" + prose

let stringifyFields (fields: FrontMatterField list) : string =
    let obj = createObj fields
    Yaml.stringify obj |> fun s -> s.TrimEnd('\n')

let compactionAnchorBody = "See above for some messages before compaction."

/// True when *text* already contains the compaction anchor body.  Used by
/// host adapters to detect that the anchor prompt has been persisted in chat
/// history and must not be re-sent.
let hasCompactionAnchorPrompt (text: string) : bool =
    not (isNull text) && text.Contains(compactionAnchorBody)

/// Whitelist of YAML keys whose front-matter blocks must survive compaction.
/// Only blocks containing at least one of these keys are carried into the
/// compaction anchor prompt; everything else is dropped to save tokens.
let compactionAnchorWhitelist =
    Set.ofList
        [ "task"
          "verdict"
          "double-check"
          "squad_event"
          "original_task"
          "review_loop_id"
          "review_round"
          "prompt_origin" ]

/// Check whether a block's YAML content contains at least one whitelist key.
let private blockHasWhitelistKey (blockContent: string) : bool =
    try
        let parsed = Yaml.parse blockContent

        if isNull parsed then
            false
        else
            objectKeys parsed
            |> Array.exists (fun k -> Set.contains k compactionAnchorWhitelist)
    with _ ->
        false

/// Render a single block content (no fence) back into a full fence string via
/// `Yaml.parse` + `frontMatterRoot` so the output is re-parseable.
let private blockToFenceString (blockContent: string) : string option =
    try
        let parsed = Yaml.parse blockContent
        if isNull parsed then None else Some(frontMatterRoot parsed)
    with _ ->
        None

/// Extract front-matter fence strings from *text*, keeping only blocks that
/// contain a whitelisted key (task / verdict / double-check / squad_event).
/// This drops ephemeral context blocks (caps, results, hints, subagent
/// prompts, etc.) to save tokens in the compaction anchor prompt.
let extractFrontMatterFenceStrings (text: string) : string list =
    if isNull text then
        []
    else
        let (blocks, _) = extractFrontMatterBlocksAndBody text
        blocks |> List.filter blockHasWhitelistKey |> List.choose blockToFenceString

/// Render a compaction-anchor prompt: append all *fenceStrings* (each already
/// a complete fence), then two newlines and `compactionAnchorBody`.  When
/// *fenceStrings* is empty the empty string is returned (caller decides
/// whether to skip emitting the anchor entirely).
let renderCompactionAnchorPrompt (fenceStrings: string list) : string =
    if List.isEmpty fenceStrings then
        ""
    else
        let fences = fenceStrings |> String.concat "\n\n"
        fences + "\n\n" + compactionAnchorBody
