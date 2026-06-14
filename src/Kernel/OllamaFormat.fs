module VibeFs.Kernel.OllamaFormat

type SearchResult = { title: string; url: string; content: string }
type FetchResponse = { title: string option; byline: string option; length: int option; content: string option }

/// Render web search results as a numbered, readable list.
let formatSearchResults (results: SearchResult list) : string =
    if results.IsEmpty then "No results found."
    else
        results
        |> List.mapi (fun i r -> $"{i + 1}. {r.title}\n   URL: {r.url}\n   {r.content}")
        |> String.concat "\n\n"

/// Render a fetched page as a labelled text block.
let formatFetchResponse (data: FetchResponse) : string =
    let nonEmpty (s: string) = not (System.String.IsNullOrEmpty s)

    let title = defaultArg data.title ""

    [ yield $"Title: {title}"
      match data.byline with
      | Some b when nonEmpty b -> yield $"By: {b}"
      | _ -> ()
      match data.length with
      | Some l -> yield $"Length: {l}"
      | None -> ()
      match data.content with
      | Some c when nonEmpty c -> yield c
      | _ -> () ]
    |> String.concat "\n"
