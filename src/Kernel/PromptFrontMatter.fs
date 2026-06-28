module Wanxiangshu.Kernel.PromptFrontMatter

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Yaml

type FrontMatterField = string * obj

[<Emit("Object.keys($0)")>]
let private objectKeys (o: obj) : string array = jsNative

[<Emit("typeof $0")>]
let private jsTypeof (o: obj) : string = jsNative

[<Emit("Object.assign($0, $1)")>]
let private objectAssign (target: obj) (source: obj) : obj = jsNative

let private extractFrontMatterBlocksAndBody (text: string) : string list * string =
    if isNull text then ([], text)
    else
        let lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
        let rec extractBlocks idx acc =
            let rec skipEmpty k =
                if k < lines.Length && System.String.IsNullOrWhiteSpace(lines.[k]) then
                    skipEmpty (k + 1)
                else k
            let startIdx = skipEmpty idx
            if startIdx < lines.Length && lines.[startIdx] = "---" then
                let rec findClose k =
                    if k >= lines.Length then -1
                    elif lines.[k] = "---" then k
                    else findClose (k + 1)
                let closeIdx = findClose (startIdx + 1)
                if closeIdx <> -1 then
                    let blockContent = String.concat "\n" lines.[startIdx + 1 .. closeIdx - 1]
                    extractBlocks (closeIdx + 1) (blockContent :: acc)
                else
                    List.rev acc, String.concat "\n" lines.[idx..]
            else
                List.rev acc, String.concat "\n" lines.[idx..]
        extractBlocks 0 []

let extractFrontMatterBlock (text: string) : string =
    let (blocks, _) = extractFrontMatterBlocksAndBody text
    match blocks with
    | [] -> ""
    | h :: _ -> h

let bodyAfterFrontMatter (text: string) : string =
    if isNull text then text
    else snd (extractFrontMatterBlocksAndBody text)

let private mergeObjs (acc: obj) (next: obj) : obj =
    if isNull acc then next
    elif isNull next then acc
    else objectAssign acc next

let parseFrontMatter (text: string) : obj =
    let (blocks, _) = extractFrontMatterBlocksAndBody text
    match blocks with
    | [] -> null
    | _ ->
        try
            blocks
            |> List.choose (fun b -> try Yaml.parse b |> Some with _ -> None)
            |> List.fold mergeObjs null
        with _ -> null

let parseFrontMatterBlocks (text: string) : obj list =
    let (blocks, _) = extractFrontMatterBlocksAndBody text
    blocks |> List.choose (fun b -> try Yaml.parse b |> Some with _ -> None)

let yamlField (key: string) (value: string) : FrontMatterField = (key, box value)

let yamlStringSeqField (key: string) (values: string list) : FrontMatterField =
    (key, box (values |> List.toArray))

let yamlSeqField (key: string) (items: obj list) : FrontMatterField =
    (key, box (items |> List.toArray))

let frontMatter (fields: FrontMatterField list) : string =
    let obj = createObj fields
    let body = Yaml.stringify obj
    "---\n" + body.TrimEnd('\n') + "\n---"

let frontMatterPrompt (fields: FrontMatterField list) (prose: string) : string =
    frontMatter fields + "\n\n" + prose

let frontMatterRoot (value: obj) : string =
    let body = Yaml.stringify value
    "---\n" + body.TrimEnd('\n') + "\n---"

let frontMatterPromptRoot (value: obj) (prose: string) : string =
    frontMatterRoot value + "\n\n" + prose

let stringifyFields (fields: FrontMatterField list) : string =
    let obj = createObj fields
    Yaml.stringify obj |> fun s -> s.TrimEnd('\n')

let parseFrontMatterScalars (text: string) : Map<string, string> =
    let parsed = parseFrontMatter text
    if isNull parsed then Map.empty
    else
        try
            objectKeys parsed
            |> Array.choose (fun key ->
                let value = parsed?(key)
                if isNull value then None
                else
                    match jsTypeof value with
                    | "string" ->
                        let s = unbox<string> value
                        let final = if s.EndsWith("\n") then s.Substring(0, s.Length - 1) else s
                        Some (key, final)
                    | "number" | "boolean" -> Some (key, string value)
                    | _ -> None)
            |> Array.fold (fun acc (k, v) -> Map.add k v acc) Map.empty
        with _ -> Map.empty

let parseFrontMatterScalarBlocks (text: string) : Map<string,string> list =
    let (blocks, _) = extractFrontMatterBlocksAndBody text
    blocks
    |> List.map (fun block -> parseFrontMatterScalars ("---\n" + block + "\n---"))

let compactionAnchorBody = "See above for some messages before compaction."

/// Whitelist of YAML keys whose front-matter blocks must survive compaction.
/// Only blocks containing at least one of these keys are carried into the
/// compaction anchor prompt; everything else is dropped to save tokens.
let compactionAnchorWhitelist =
    Set.ofList ["task"; "verdict"; "double-check"; "squad_event"]

/// Check whether a block's YAML content contains at least one whitelist key.
let private blockHasWhitelistKey (blockContent: string) : bool =
    try
        let parsed = Yaml.parse blockContent
        if isNull parsed then false
        else
            objectKeys parsed
            |> Array.exists (fun k -> Set.contains k compactionAnchorWhitelist)
    with _ -> false

/// Render a single block content (no fence) back into a full fence string via
/// `Yaml.parse` + `frontMatterRoot` so the output is re-parseable.
let private blockToFenceString (blockContent: string) : string option =
    try
        let parsed = Yaml.parse blockContent
        if isNull parsed then None else Some (frontMatterRoot parsed)
    with _ -> None

/// Extract front-matter fence strings from *text*, keeping only blocks that
/// contain a whitelisted key (task / verdict / double-check / squad_event).
/// This drops ephemeral context blocks (caps, results, hints, subagent
/// prompts, etc.) to save tokens in the compaction anchor prompt.
let extractFrontMatterFenceStrings (text: string) : string list =
    if isNull text then []
    else
        let (blocks, _) = extractFrontMatterBlocksAndBody text
        blocks
        |> List.filter blockHasWhitelistKey
        |> List.choose blockToFenceString

/// Render a compaction-anchor prompt: append all *fenceStrings* (each already
/// a complete fence), then two newlines and `compactionAnchorBody`.  When
/// *fenceStrings* is empty the body is returned as-is.
let renderCompactionAnchorPrompt (fenceStrings: string list) : string =
    if List.isEmpty fenceStrings then
        compactionAnchorBody
    else
        let fences = fenceStrings |> String.concat "\n\n"
        fences + "\n\n" + compactionAnchorBody
