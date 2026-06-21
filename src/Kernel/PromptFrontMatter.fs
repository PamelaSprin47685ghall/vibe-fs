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

/// Inverse of `yamlScalar`: strip the surrounding quotes and unescape. Returns
/// None when the value is not a quoted scalar (e.g. a `key: |` block header), so
/// callers scanning for scalar anchors naturally ignore block fields.
let parseYamlScalar (raw: string) : string option =
    let t = raw.Trim()
    if t.Length >= 2 && t.StartsWith("\"") && t.EndsWith("\"") then
        Some(t.Substring(1, t.Length - 2).Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\"))
    else None

/// Parse the top-level scalar fields of the YAML front-matter block that opens
/// `text`. Only a block whose first line is exactly `---` and that closes with a
/// later `---` line is recognized, and only un-indented `key: "value"` scalars
/// are returned — indented block-field bodies (and any `---` within them) are
/// skipped. Ordinary prose, which practically never opens with a `---` fence,
/// yields an empty map, making these fields a collision-free state anchor.
let parseFrontMatterScalars (text: string) : Map<string, string> =
    if isNull text then Map.empty
    else
        let lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
        if lines.Length < 2 || lines.[0] <> "---" then Map.empty
        else
            let rec loop i acc =
                if i >= lines.Length then Map.empty
                elif lines.[i] = "---" then acc
                else
                    let line = lines.[i]
                    let acc =
                        if line.Length > 0 && line.[0] <> ' ' && line.[0] <> '\t' then
                            let sep = line.IndexOf(": ")
                            if sep > 0 then
                                match parseYamlScalar (line.Substring(sep + 2)) with
                                | Some value -> Map.add (line.Substring(0, sep)) value acc
                                | None -> acc
                            else acc
                        else acc
                    loop (i + 1) acc
            loop 1 Map.empty
