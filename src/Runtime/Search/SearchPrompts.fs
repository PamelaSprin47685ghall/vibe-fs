module Wanxiangshu.Runtime.SearchPrompts

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.PromptHeader

type SearchResult =
    { title: string
      url: string
      content: string }

type FetchResponse =
    { title: string option
      byline: string option
      length: int option
      content: string option }

let formatSearchResults (results: SearchResult list) : string =
    if results.IsEmpty then
        "No results found."
    else
        let items =
            results
            |> List.map (fun r -> createObj [ "title", box r.title; "url", box r.url; "content", box r.content ])

        frontMatter [ yamlSeqField "results" items ]

let formatFetchResponse (data: FetchResponse) : string =
    let nonEmpty (s: string) = not (System.String.IsNullOrEmpty s)

    let fields =
        [ match data.title with
          | Some v when nonEmpty v -> yield yamlField "title" v
          | _ -> ()
          match data.byline with
          | Some v when nonEmpty v -> yield yamlField "byline" v
          | _ -> ()
          match data.length with
          | Some l -> yield ("length", box l)
          | None -> ()
          match data.content with
          | Some c when nonEmpty c -> yield yamlField "content" c
          | _ -> () ]

    frontMatter fields
