module VibeFs.Kernel.SearchPrompts

open VibeFs.Kernel.PromptFrontMatter

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
            |> List.map (fun r ->
                let contentBlock = yamlField "content" r.content

                let indentedContentBlock =
                    contentBlock.Split('\n')
                    |> Array.map (fun line -> "    " + line)
                    |> String.concat "\n"

                "  - title: "
                + yamlStringValue r.title
                + "\n    url: "
                + yamlStringValue r.url
                + "\n"
                + indentedContentBlock)

        frontMatter [ yamlSeqField "results" items ]

let formatFetchResponse (data: FetchResponse) : string =
    let nonEmpty (s: string) = not (System.String.IsNullOrEmpty s)

    let scalarIf (key: string) =
        function
        | Some v when nonEmpty v -> [ yamlField key v ]
        | _ -> []

    let title = scalarIf "title" data.title
    let byline = scalarIf "byline" data.byline

    let length =
        match data.length with
        | Some l -> [ yamlField "length" (string l) ]
        | None -> []

    let content =
        match data.content with
        | Some c when nonEmpty c -> [ yamlField "content" c ]
        | _ -> []

    frontMatter (title @ byline @ length @ content)
