module Wanxiangshu.Runtime.PromptHeader

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Yaml

type HeaderField = string * obj
type FrontMatterField = HeaderField

let yamlField (key: string) (value: string) : HeaderField = (key, box value)

let yamlStringSeqField (key: string) (values: string list) : HeaderField = (key, box (values |> List.toArray))

let yamlSeqField (key: string) (items: obj list) : HeaderField = (key, box (items |> List.toArray))

let promptHeader (fields: HeaderField list) : string =
    if List.isEmpty fields then
        ""
    else
        let obj = createObj fields
        let body = Yaml.stringify obj
        "---\n" + body.TrimEnd('\n') + "\n---"

let frontMatter = promptHeader

let promptHeaderPrompt (fields: HeaderField list) (prose: string) : string =
    if List.isEmpty fields then
        prose
    else
        promptHeader fields + "\n\n" + prose

let frontMatterPrompt = promptHeaderPrompt

let promptHeaderRoot (value: obj) : string =
    let body = Yaml.stringify value
    "---\n" + body.TrimEnd('\n') + "\n---"

let frontMatterRoot = promptHeaderRoot

let promptHeaderPromptRoot (value: obj) (prose: string) : string = promptHeaderRoot value + "\n\n" + prose

let frontMatterPromptRoot = promptHeaderPromptRoot

let stringifyFields (fields: HeaderField list) : string =
    let obj = createObj fields
    Yaml.stringify obj |> fun s -> s.TrimEnd('\n')
