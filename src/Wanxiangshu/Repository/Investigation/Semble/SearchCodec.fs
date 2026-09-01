namespace Wanxiangshu.Repository.Investigation.Semble

open System
open Fable.Core
open Fable.Core.JsInterop

/// AGENT-027: MCP tools/call payload → SembleMcp.Hit list. Pure.
module SembleSearchCodec =

    let private intFromKnown (value: obj) : int option =
        match unbox<obj> value with
        | :? float as f -> Some(int f)
        | :? int as n -> Some n
        | _ -> None

    let private intFromParse (value: obj) : int option =
        match Int32.TryParse(string value) with
        | true, n -> Some n
        | _ -> None

    let private asInt (value: obj) (fallback: int) =
        if isNull value then
            fallback
        else
            intFromKnown value
            |> Option.orElseWith (fun () -> intFromParse value)
            |> Option.defaultValue fallback

    let private floatFromKnown (value: obj) : float option =
        match unbox<obj> value with
        | :? float as f -> Some f
        | :? int as n -> Some(float n)
        | _ -> None

    let private floatFromParse (value: obj) : float option =
        match Double.TryParse(string value) with
        | true, f -> Some f
        | _ -> None

    let private asFloat (value: obj) (fallback: float) =
        if isNull value then
            fallback
        else
            floatFromKnown value
            |> Option.orElseWith (fun () -> floatFromParse value)
            |> Option.defaultValue fallback

    let private asString (value: obj) =
        if isNull value then "" else string value

    let private snippetLines (content: string) =
        if String.IsNullOrEmpty content then
            0
        else
            content.Split([| '\n' |], StringSplitOptions.None).Length

    let private resolveTotalLines (declared: int) (content: string) (endLine: int) =
        if declared > 0 then
            declared
        else
            max (snippetLines content) endLine

    let private hitFromNonNull (item: obj) : SembleMcp.Hit option =
        let filePath = asString item?file_path

        if String.IsNullOrWhiteSpace filePath then
            None
        else
            let content = asString item?content
            let startLine = asInt item?start_line 1
            let endLine = asInt item?end_line startLine
            let score = asFloat item?score 0.0
            let declared = asInt item?total_lines 0

            Some
                { FilePath = filePath
                  StartLine = startLine
                  EndLine = endLine
                  Content = content
                  Score = score
                  TotalLines = resolveTotalLines declared content endLine }

    let private hitFrom (item: obj) : SembleMcp.Hit option =
        if isNull item then None else hitFromNonNull item

    let private hitsFromParsed (parsed: obj) : SembleMcp.Hit list =
        let results = parsed?results

        if isNull results then
            []
        else
            let items: obj array = unbox results
            items |> Array.choose hitFrom |> Array.toList

    let private parseTextBody (text: string) : SembleMcp.Hit list =
        try
            hitsFromParsed (JS.JSON.parse text)
        with _ ->
            []

    let parseText (text: string) : SembleMcp.Hit list =
        if String.IsNullOrWhiteSpace text then
            []
        else
            parseTextBody text

    let private parseToolContent (content: obj) : SembleMcp.Hit list =
        let items: obj array = unbox content

        if isNull items || items.Length = 0 then []
        elif isNull items.[0] then []
        else parseText (asString items.[0]?text)

    let private parseToolResultBody (result: obj) : SembleMcp.Hit list =
        try
            let content = result?content
            if isNull content then [] else parseToolContent content
        with _ ->
            []

    let parseToolResult (result: obj) : SembleMcp.Hit list =
        if isNull result then [] else parseToolResultBody result
