module Wanxiangshu.Kernel.TreeSitterKernel

open System.Text.RegularExpressions

/// One syntax problem located in a source file.
type SyntaxDiagnostic =
    { line: int
      column: int
      endLine: int
      endColumn: int
      severity: string
      message: string }

/// The outcome of checking a file's syntax — either an array of diagnostics with
/// the detected language, or a failure reason.
type SyntaxCheckResult =
    | Ok of lang: string * errors: SyntaxDiagnostic array
    | Failed of lang: string * reason: string

type AstNodeInfo =
    { kind: string
      startLine: int
      endLine: int }

type StyleLimits =
    { maxFileLines: int
      maxFunctionLines: int
      functionKinds: Set<string> }

let defaultStyleLimits: StyleLimits =
    { maxFileLines = 300
      maxFunctionLines = 60
      functionKinds =
        Set.ofList
            [ // General tree-sitter grammar names
              "function"
              "function_definition"
              "function_declaration"
              "method_definition"
              "method_declaration"
              "arrow_function"
              "let_binding"
              "value_declaration"
              // F# tree-sitter grammar names
              "function_or_value_defn"
              "function_expression"
              "fun_expression"
              "value_declaration_left" ] }

let warningFunctionLines = 50

let checkFileLineCount (limits: StyleLimits) (content: string) : SyntaxDiagnostic[] =
    if System.String.IsNullOrEmpty content then
        [||]
    else
        let lines = content.Split('\n')
        let count = lines.Length

        if count > limits.maxFileLines then
            [| { line = 1
                 column = 1
                 endLine = 1
                 endColumn = 1
                 severity = "error"
                 message =
                   $"File exceeds 300 lines (currently {count} lines). Do not compress code to bypass length limits; you must split files." } |]
        elif count > 200 then
            [| { line = 1
                 column = 1
                 endLine = 1
                 endColumn = 1
                 severity = "warning"
                 message =
                   $"File exceeds 200 lines (currently {count} lines). Do not compress code to bypass length limits; you must split files." } |]
        else
            [||]

let checkFunctionLengths (limits: StyleLimits) (nodes: AstNodeInfo[]) : SyntaxDiagnostic[] =
    nodes
    |> Array.filter (fun node -> limits.functionKinds.Contains node.kind)
    |> Array.choose (fun node ->
        let len = node.endLine - node.startLine + 1

        if len > limits.maxFunctionLines then
            Some
                { line = node.startLine
                  column = 1
                  endLine = node.endLine
                  endColumn = 1
                  severity = "error"
                  message =
                    $"Function exceeds 60 lines (currently {len} lines). Do not compress code to bypass length limits; you must split functions." }
        elif len > warningFunctionLines then
            Some
                { line = node.startLine
                  column = 1
                  endLine = node.endLine
                  endColumn = 1
                  severity = "warning"
                  message =
                    $"Function exceeds 50 lines (currently {len} lines). Do not compress code to bypass length limits; you must split functions." }
        else
            None)

/// The marker string placed on the line immediately before a function or value
/// binding to exempt it from architecture gate length checks.
let architectureExemptionMarker = "ARCHITECTURE_EXEMPT"

/// Filter out diagnostics whose source line is preceded by an exemption
/// comment marker. The marker must appear on the line before the diagnostic's
/// start line (1-indexed), possibly preceded by whitespace + //.
let filterExemptedDiagnostics (content: string) (diags: SyntaxDiagnostic[]) : SyntaxDiagnostic[] =
    if System.String.IsNullOrEmpty content || Array.isEmpty diags then
        diags
    else
        let lines = content.Split('\n')

        let hasExemption (lineIndex: int) =
            lineIndex >= 0
            && lineIndex < lines.Length
            && lines.[lineIndex].Contains architectureExemptionMarker

        diags
        |> Array.filter (fun d ->
            let precedingLineIndex = d.line - 2
            let sameLineIndex = d.line - 1
            not (hasExemption precedingLineIndex || hasExemption sameLineIndex))

/// Pure parser over a patchText blob: extracts every `*** Add File|Update
/// File|Move to: <path>` target, de-duplicated, order-preserved.
let private patchPathRe = Regex(@"^\*\*\* (?:Add File|Update File|Move to): (.+)$")

let pathsFromPatchText (patchText: string) : string list =
    if System.String.IsNullOrEmpty patchText then
        []
    else
        patchText.Split('\n')
        |> Seq.choose (fun line ->
            let m = patchPathRe.Match line
            if m.Success then Some m.Groups.[1].Value else None)
        |> List.ofSeq
        |> List.distinct

/// Format a syntax-check result into a human-readable annotation, or None when
/// the file is clean and `includeOk` is false.
let formatSyntaxDiagnostics (filePath: string) (result: SyntaxCheckResult) (includeOk: bool) : string option =
    match result with
    | Failed(lang, reason) ->
        let langLabel = if lang = "" then "unknown" else lang
        Some($"Syntax check failed in {filePath} ({langLabel}): {reason}")
    | Ok(lang, errors) ->
        if Array.isEmpty errors then
            if includeOk then Some($"{filePath}: ok ({lang})") else None
        else
            let header = $"{errors.Length} syntax issue(s) in {filePath} ({lang})"

            let body =
                errors
                |> Array.map (fun e -> $"  L{e.line}:{e.column}-{e.endLine}:{e.endColumn} [{e.severity}] {e.message}")
                |> Array.toList

            Some(String.concat "\n" (header :: body))
