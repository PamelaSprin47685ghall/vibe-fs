module Wanxiangshu.Runtime.OmpPromptContext

open Wanxiangshu.Runtime.Dyn

let private capsMarker = "<caps-context"

let escapeXmlAttr (value: string) : string =
    value
        .Replace("&", "&amp;")
        .Replace("\"", "&quot;")
        .Replace("'", "&apos;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")

let formatOmpCapsContext (files: (string * string) list) : string =
    if List.isEmpty files then
        ""
    else
        files
        |> List.map (fun f -> $"<caps-context file=\"{escapeXmlAttr (fst f)}\">\n{snd f}\n</caps-context>")
        |> String.concat "\n\n"

let private normalizeSystemPrompt (systemPrompt: obj) : string array =
    if isNull systemPrompt then
        [||]
    elif Dyn.isArray systemPrompt then
        systemPrompt :?> obj array |> Array.map string
    else
        [| string systemPrompt |]

let private stripDirContextSegment (segment: string) : string option =
    let pattern = Regex("<dir-context>[\s\S]*?</dir-context>", RegexOptions.IgnoreCase)
    let stripped = pattern.Replace(segment, "").Trim()
    if stripped = "" then None else Some stripped

let stripHostAgentsPrompt (systemPrompt: obj) : string array =
    normalizeSystemPrompt systemPrompt |> Array.choose stripDirContextSegment

let private hasCapsContext (parts: string array) =
    parts |> Array.exists (fun s -> s.Contains capsMarker)
