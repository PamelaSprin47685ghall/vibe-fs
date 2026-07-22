module Wanxiangshu.Runtime.SearchPrompts

open Wanxiangshu.Runtime.Tooling.ToolOutputToml

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
    let items =
        results
        |> List.map (fun r ->
            { SearchResultItem.title = r.title
              url = r.url
              content = r.content })

    renderSearchResults items

let formatFetchResponse (data: FetchResponse) : string =
    let nonEmpty (s: string) = not (System.String.IsNullOrEmpty s)

    let fetch =
        { FetchResult.title = data.title |> Option.filter nonEmpty
          byline = data.byline |> Option.filter nonEmpty
          length = data.length
          content = data.content |> Option.filter nonEmpty }

    renderFetchResult fetch
