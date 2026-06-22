module VibeFs.Kernel.PromptFrontMatter

/// Shared YAML front-matter markdown builders for prompts. Pure string
/// composition: structured fields go in the front matter, free-text prose
/// follows after. Presentation only — no persistence coupling.

let yamlScalar (value: string) : string =
    "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\""

let yamlScalarField (key: string) (value: string) : string =
    key + ": " + yamlScalar value

let yamlPlainField (key: string) (value: string) : string =
    key + ": " + value

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

/// Parse the top-level scalar-like fields of the YAML front-matter block that
/// opens `text`. Supports quoted scalars (`key: "value"`), literal block
/// scalars (`key: |` with two-space-indented body), and plain unquoted simple
/// scalars (`key: value`). The opening fence MUST be closed by a later top-level
/// `---`; otherwise the result is empty. Ordinary prose, which practically never
/// opens with a `---` fence, likewise yields an empty map, making these fields a
/// collision-free state anchor.
let parseFrontMatterScalars (text: string) : Map<string, string> =
    if isNull text then
        Map.empty
    else
        let lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
        if lines.Length < 2 || lines.[0] <> "---" then
            Map.empty
        else
            let readBlock startIndex =
                let rec gather j acc =
                    if j >= lines.Length then
                        String.concat "\n" (List.rev acc), j
                    else
                        let line = lines.[j]
                        if line = "" then
                            gather (j + 1) ("" :: acc)
                        elif line.StartsWith("  ") then
                            gather (j + 1) (line.Substring(2) :: acc)
                        else
                            String.concat "\n" (List.rev acc), j
                gather startIndex []

            let rec loop i acc : Map<string, string> option =
                if i >= lines.Length then
                    None
                elif lines.[i] = "---" then
                    Some acc
                else
                    let line = lines.[i]
                    if line.Length = 0 || line.[0] = ' ' || line.[0] = '\t' then
                        loop (i + 1) acc
                    else
                        let sep = line.IndexOf(": ")
                        if sep <= 0 then
                            loop (i + 1) acc
                        else
                            let key = line.Substring(0, sep)
                            let raw = line.Substring(sep + 2)
                            match parseYamlScalar raw with
                            | Some value ->
                                loop (i + 1) (Map.add key value acc)
                            | None when raw = "|" ->
                                let value, nextIndex = readBlock (i + 1)
                                loop nextIndex (Map.add key value acc)
                            | None ->
                                let value = raw.Trim()
                                if value = "" then
                                    loop (i + 1) acc
                                else
                                    loop (i + 1) (Map.add key value acc)

            loop 1 Map.empty |> Option.defaultValue Map.empty
