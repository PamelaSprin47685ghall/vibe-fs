module Wanxiangshu.Kernel.PromptFrontMatter

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Yaml

type FrontMatterField = string * obj

[<Emit("Object.keys($0)")>]
let private objectKeys (o: obj) : string array = jsNative

[<Emit("typeof $0")>]
let private jsTypeof (o: obj) : string = jsNative

let private extractFrontMatterBlock (text: string) : string =
    if isNull text then ""
    else
        let lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
        if lines.Length < 2 || lines.[0] <> "---" then ""
        else
            let rec findClose i =
                if i >= lines.Length then ""
                elif lines.[i] = "---" then String.concat "\n" lines.[1 .. i - 1]
                else findClose (i + 1)
            findClose 1

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

let parseFrontMatter (text: string) : obj =
    let fm = extractFrontMatterBlock text
    if fm = "" then null
    else try Yaml.parse fm with _ -> null

let bodyAfterFrontMatter (text: string) : string =
    if isNull text then text
    else
        let lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
        if lines.Length < 2 || lines.[0] <> "---" then text
        else
            let rec findClose i =
                if i >= lines.Length then text
                elif lines.[i] = "---" then
                    if i + 1 >= lines.Length then ""
                    else String.concat "\n" lines.[i + 1 ..]
                else findClose (i + 1)
            findClose 1

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
