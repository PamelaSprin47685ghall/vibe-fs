module VibeFs.Kernel.SearchPrompts

open VibeFs.Kernel.Dyn
open VibeFs.Kernel.PromptFrontMatter

type SearchResult =
    { title: string
      url: string
      content: string }

let parseSearchResults (results: obj) : SearchResult list =
    if Dyn.isNullish results || not (Dyn.isArray results) then
        []
    else
        (results :?> obj array)
        |> Array.map (fun r ->
            { title = Dyn.str r "title"
              url = Dyn.str r "url"
              content = Dyn.str r "content" })
        |> List.ofArray

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
                let contentBlock = yamlBlockField "content" r.content

                let indentedContentBlock =
                    contentBlock.Split('\n')
                    |> Array.map (fun line -> "    " + line)
                    |> String.concat "\n"

                "  - title: "
                + yamlScalar r.title
                + "\n    url: "
                + yamlScalar r.url
                + "\n"
                + indentedContentBlock)

        frontMatter [ yamlSeqField "results" items ]

let formatFetchResponse (data: FetchResponse) : string =
    let nonEmpty (s: string) = not (System.String.IsNullOrEmpty s)

    let scalarIf (key: string) =
        function
        | Some v when nonEmpty v -> [ yamlScalarField key v ]
        | _ -> []

    let title = scalarIf "title" data.title
    let byline = scalarIf "byline" data.byline

    let length =
        match data.length with
        | Some l -> [ yamlScalarField "length" (string l) ]
        | None -> []

    let content =
        match data.content with
        | Some c when nonEmpty c -> [ yamlBlockField "content" c ]
        | _ -> []

    frontMatter (title @ byline @ length @ content)
