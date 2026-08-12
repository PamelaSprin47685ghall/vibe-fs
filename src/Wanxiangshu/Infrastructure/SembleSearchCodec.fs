namespace Wanxiangshu.Infrastructure

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel

/// AGENT-027: MCP tools/call payload → SembleMcp.Hit list. Pure.
module SembleSearchCodec =

    let private asInt (value: obj) (fallback: int) =
        if isNull value then
            fallback
        else
            match unbox<obj> value with
            | :? float as f -> int f
            | :? int as n -> n
            | other ->
                match Int32.TryParse(string other) with
                | true, n -> n
                | _ -> fallback

    let private asFloat (value: obj) (fallback: float) =
        if isNull value then
            fallback
        else
            match unbox<obj> value with
            | :? float as f -> f
            | :? int as n -> float n
            | other ->
                match Double.TryParse(string other) with
                | true, f -> f
                | _ -> fallback

    let private asString (value: obj) =
        if isNull value then "" else string value

    let private snippetLines (content: string) =
        if String.IsNullOrEmpty content then
            0
        else
            content.Split([| '\n' |], StringSplitOptions.None).Length

    let private hitFrom (item: obj) : SembleMcp.Hit option =
        if isNull item then
            None
        else
            let filePath = asString item?file_path

            if String.IsNullOrWhiteSpace filePath then
                None
            else
                let content = asString item?content
                let startLine = asInt item?start_line 1
                let endLine = asInt item?end_line startLine
                let score = asFloat item?score 0.0
                let declared = asInt item?total_lines 0

                let totalLines =
                    if declared > 0 then
                        declared
                    else
                        max (snippetLines content) endLine

                Some
                    { FilePath = filePath
                      StartLine = startLine
                      EndLine = endLine
                      Content = content
                      Score = score
                      TotalLines = totalLines }

    let parseText (text: string) : SembleMcp.Hit list =
        if String.IsNullOrWhiteSpace text then
            []
        else
            try
                let parsed = JS.JSON.parse text
                let results = parsed?results

                if isNull results then
                    []
                else
                    let items: obj array = unbox results

                    [ for item in items do
                          match hitFrom item with
                          | Some hit -> yield hit
                          | None -> () ]
            with _ ->
                []

    let parseToolResult (result: obj) : SembleMcp.Hit list =
        if isNull result then
            []
        else
            try
                let content = result?content

                if isNull content then
                    []
                else
                    let items: obj array = unbox content

                    if isNull items || items.Length = 0 then
                        []
                    else
                        let first = items.[0]
                        if isNull first then [] else parseText (asString first?text)
            with _ ->
                []
