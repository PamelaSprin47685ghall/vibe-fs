module VibeFs.Kernel.PromptFrontMatter

/// Shared YAML front-matter markdown builders for prompts. Pure string
/// composition: structured fields go in the front matter, free-text prose
/// follows after. Presentation only — no persistence coupling.

let yamlScalar (value: string) : string =
    "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\""

let yamlScalarField (key: string) (value: string) : string =
    key + ": " + yamlScalar value

let yamlBlockField (key: string) (value: string) : string =
    let body = value.Split('\n') |> Array.map (fun line -> "  " + line) |> String.concat "\n"
    key + ": |\n" + body

/// `items` are pre-rendered, already-indented YAML sequence entries
/// (each must start with `  - `). Empty list renders `key: []`.
let yamlSeqField (key: string) (items: string list) : string =
    if items.IsEmpty then key + ": []" else key + ":\n" + String.concat "\n" items

let yamlStringSeqField (key: string) (values: string list) : string =
    yamlSeqField key (values |> List.map (fun v -> "  - " + yamlScalar v))

let frontMatter (fields: string list) : string =
    "---\n" + String.concat "\n" fields + "\n---"

let frontMatterPrompt (fields: string list) (prose: string) : string =
    frontMatter fields + "\n\n" + prose
