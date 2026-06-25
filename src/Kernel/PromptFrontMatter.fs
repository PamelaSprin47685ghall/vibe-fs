module VibeFs.Kernel.PromptFrontMatter

/// Shared YAML front-matter builders. All scalar encoding goes through `yamlStringValue`.

let private normalized (value: string) : string =
    if isNull value then "" else value.Replace("\r\n", "\n").Replace("\r", "\n")

let private hasLineBreak (value: string) : bool =
    let s = normalized value
    s.Contains("\n")

let private isYamlNullWord (s: string) : bool =
    match s.ToLowerInvariant() with
    | "null" | "~" -> true
    | _ -> false

let private isYamlBoolWord (s: string) : bool =
    match s.ToLowerInvariant() with
    | "true" | "false" -> true
    | _ -> false

let private isDocumentMarker (s: string) : bool =
    s = "---" || s = "..."

let private startsWithYamlIndicator (c: char) : bool =
    c = '-'
    || c = '?'
    || c = ':'
    || c = ','
    || c = '['
    || c = ']'
    || c = '{'
    || c = '}'
    || c = '&'
    || c = '*'
    || c = '!'
    || c = '|'
    || c = '>'
    || c = '\''
    || c = '%'
    || c = '@'
    || c = '`'

let private needsQuotes (value: string) : bool =
    let s = normalized value
    if s = "" then true
    elif s <> s.Trim() then true
    elif s.Contains("\"") || s.Contains("\\") then true
    elif s.Contains("#") then true
    elif s.Length > 0 && startsWithYamlIndicator s.[0] then true
    elif s.Contains(": ") then true
    elif isYamlBoolWord s || isYamlNullWord s || isDocumentMarker s then true
    else false

let private quoteDouble (value: string) : string =
    "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""

let private blockBody (value: string) (linePrefix: string) : string =
    normalized value
    |> fun s -> s.Split('\n')
    |> Array.map (fun line -> linePrefix + line)
    |> String.concat "\n"

/// Runtime scalar encoding: plain (unquoted) → double-quoted → literal block (only when `\\n` present).
let yamlStringValue (value: string) : string =
    let s = normalized value
    if hasLineBreak s then blockBody s "  "
    elif needsQuotes s then quoteDouble s
    else s

let private yamlKeyedField (linePrefix: string) (key: string) (value: string) (blockLinePrefix: string) : string =
    let s = normalized value
    if hasLineBreak s then
        linePrefix + key + ": |\n" + blockBody s blockLinePrefix
    else
        linePrefix + key + ": " + yamlStringValue s

/// `key: value` at document indent (no leading spaces on the key line).
let yamlField (key: string) (value: string) : string = yamlKeyedField "" key value "  "

/// List item `  - key: value` or `  - key: |` with body indented under the item.
let yamlListItemField (key: string) (value: string) (listMarkerIndent: string) : string =
    yamlKeyedField (listMarkerIndent + "- ") key value (listMarkerIndent + "  ")

let yamlSeqField (key: string) (items: string list) : string =
    if items.IsEmpty then key + ": []" else key + ":\n" + String.concat "\n" items

let yamlStringSeqField (key: string) (values: string list) : string =
    yamlSeqField key (values |> List.map (fun v -> "  - " + yamlStringValue v))

let frontMatter (fields: string list) : string =
    "---\n" + String.concat "\n" fields + "\n---"

let frontMatterPrompt (fields: string list) (prose: string) : string =
    frontMatter fields + "\n\n" + prose

let parseYamlStringValue (raw: string) : string option =
    let t = raw.Trim()
    if t.Length >= 2 && t.StartsWith("\"") && t.EndsWith("\"") then
        Some(t.Substring(1, t.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\"))
    else
        None

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
                            match parseYamlStringValue raw with
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