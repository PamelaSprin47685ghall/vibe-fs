module Wanxiangshu.Runtime.SubagentReportParse

/// Recovery codec for untrusted free-text subagent replies.
/// Typed spawn paths already carry SubagentReport; this only recovers lightly-keyed
/// fields when a host returns prose / partial TOML-shaped text.
open System
open Wanxiangshu.Runtime.Tooling.ToolOutputBatchToml

let private trim (s: string) = if isNull s then "" else s.Trim()

let private extractQuotedStrings (inner: string) : string list =
    let rec loop (s: string) (acc: string list) =
        let s = s.TrimStart()

        if s = "" || s.[0] <> '"' then
            List.rev acc
        else
            let rec scan i escaped =
                if i >= s.Length then
                    None
                elif escaped then
                    scan (i + 1) false
                elif s.[i] = '\\' then
                    scan (i + 1) true
                elif s.[i] = '"' then
                    Some i
                else
                    scan (i + 1) false

            match scan 1 false with
            | None -> List.rev acc
            | Some endQuote ->
                let raw = s.Substring(1, endQuote - 1).Replace("\\\"", "\"").Replace("\\\\", "\\")
                let rest = s.Substring(endQuote + 1).TrimStart()
                let rest = if rest.StartsWith(",") then rest.Substring(1) else rest
                loop rest (raw :: acc)

    loop inner []

/// Extract a TOML-style string-array value for `key = [ "a", "b" ]` from free text.
let private stringArrayForKey (key: string) (text: string) : string list =
    let needle = key + " ="
    let idx = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase)

    if idx < 0 then
        []
    else
        let after = text.Substring(idx + needle.Length).TrimStart()

        if not (after.StartsWith("[")) then
            []
        else
            let close = after.IndexOf(']')
            if close <= 0 then [] else extractQuotedStrings (after.Substring(1, close - 1))

let private stringForKey (key: string) (text: string) : string option =
    let needle = key + " ="
    let idx = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase)

    if idx < 0 then
        None
    else
        let after = text.Substring(idx + needle.Length).TrimStart()

        if after.StartsWith("\"") then
            let rest = after.Substring(1)
            let endQuote = rest.IndexOf('"')
            if endQuote < 0 then None else Some(rest.Substring(0, endQuote))
        else
            let lineEnd =
                match after.IndexOf('\n') with
                | -1 -> after.Length
                | n -> n

            let value = after.Substring(0, lineEnd).Trim()
            if value = "" then None else Some value

let private firstSummaryLine (trimmed: string) : string option =
    trimmed.Split('\n')
    |> Array.map trim
    |> Array.tryFind (fun line ->
        line <> ""
        && not (line.StartsWith("findings", StringComparison.OrdinalIgnoreCase))
        && not (line.StartsWith("related_", StringComparison.OrdinalIgnoreCase))
        && not (line.StartsWith("relatedFiles", StringComparison.OrdinalIgnoreCase))
        && not (line.StartsWith("relatedCode", StringComparison.OrdinalIgnoreCase)))

/// Parse free-form or lightly-structured subagent report text into SubagentReport.
let parseSubagentReportText (text: string) : SubagentReport =
    let trimmed = trim text

    if trimmed = "" then
        { iterator = None
          summary = None
          error = None
          findings = []
          relatedFiles = []
          relatedCode = [] }
    else
        let findings = stringArrayForKey "findings" trimmed
        let relatedFiles = stringArrayForKey "related_files" trimmed
        let relatedCode = stringArrayForKey "related_code" trimmed
        let filesAlt = stringArrayForKey "relatedFiles" trimmed
        let codeAlt = stringArrayForKey "relatedCode" trimmed
        let files = if List.isEmpty relatedFiles then filesAlt else relatedFiles
        let code = if List.isEmpty relatedCode then codeAlt else relatedCode

        let summaryOpt =
            match stringForKey "summary" trimmed with
            | Some s -> Some s
            | None when List.isEmpty findings && List.isEmpty files && List.isEmpty code -> Some trimmed
            | None -> firstSummaryLine trimmed

        { iterator = stringForKey "iterator" trimmed
          summary = summaryOpt
          error = None
          findings = findings
          relatedFiles = files
          relatedCode = code }
