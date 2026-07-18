module Wanxiangshu.Runtime.PromptFrontMatterParse

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Yaml

[<Global("Object")>]
let private JSObject: obj = jsNative

let internal objectKeys (o: obj) : string array = JSObject?keys(o) |> unbox

let internal jsTypeof (o: obj) : string =
    if isNull o then
        "object"
    else
        match o with
        | :? string -> "string"
        | :? bool -> "boolean"
        | :? int
        | :? float -> "number"
        | _ -> "object"

let private objectAssign (target: obj) (source: obj) : obj = JSObject?assign(target, source)

let internal extractFrontMatterBlocksAndBody (text: string) : string list * string =
    if isNull text then
        ([], text)
    elif not (text.Contains "---") then
        ([], text)
    else
        let lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')

        let rec extractBlocks idx acc =
            let rec skipEmpty k =
                if k < lines.Length && System.String.IsNullOrWhiteSpace(lines.[k]) then
                    skipEmpty (k + 1)
                else
                    k

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
    if isNull text then
        text
    else
        snd (extractFrontMatterBlocksAndBody text)

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
            |> List.choose (fun b ->
                try
                    Yaml.parse b |> Some
                with _ ->
                    None)
            |> List.fold mergeObjs null
        with _ ->
            null

let parseFrontMatterBlocks (text: string) : obj list =
    let (blocks, _) = extractFrontMatterBlocksAndBody text

    blocks
    |> List.choose (fun b ->
        try
            Yaml.parse b |> Some
        with _ ->
            None)

let parseFrontMatterScalars (text: string) : Map<string, string> =
    let parsed = parseFrontMatter text

    if isNull parsed then
        Map.empty
    else
        try
            objectKeys parsed
            |> Array.choose (fun key ->
                let value = parsed?(key)

                if isNull value then
                    None
                else
                    match jsTypeof value with
                    | "string" ->
                        let s = unbox<string> value
                        let final = if s.EndsWith("\n") then s.Substring(0, s.Length - 1) else s
                        Some(key, final)
                    | "number"
                    | "boolean" -> Some(key, string value)
                    | _ -> None)
            |> Array.fold (fun acc (k, v) -> Map.add k v acc) Map.empty
        with _ ->
            Map.empty

let parseFrontMatterScalarBlocks (text: string) : Map<string, string> list =
    let (blocks, _) = extractFrontMatterBlocksAndBody text

    blocks
    |> List.map (fun block -> parseFrontMatterScalars ("---\n" + block + "\n---"))
