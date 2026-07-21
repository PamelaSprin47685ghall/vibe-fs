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
