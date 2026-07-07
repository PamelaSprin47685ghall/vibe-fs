module Wanxiangshu.Kernel.TreeSitterKernel

open Wanxiangshu.Kernel.ToolOutputInfo

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
    { maxLineLength: int
      maxFileLines: int
      maxFunctionLines: int
      functionKinds: Set<string> }

let defaultStyleLimits: StyleLimits =
    { maxLineLength = 72
      maxFileLines = 300
      maxFunctionLines = 50
      functionKinds =
        Set.ofList [
            "function"
            "function_definition"
            "function_declaration"
            "method_definition"
            "method_declaration"
            "arrow_function"
            "let_binding"
            "value_declaration"
        ] }

let charWidth (c: char) : int =
    if int c > 127 then 2 else 1

let stringWidth (s: string) : int =
    let mutable w = 0
    for i = 0 to s.Length - 1 do
        w <- w + charWidth s.[i]
    w

let checkLineLengths (limits: StyleLimits) (content: string) : SyntaxDiagnostic[] =
    if System.String.IsNullOrEmpty content then [||]
    else
        let lines = content.Split('\n')
        lines
        |> Array.mapi (fun idx line ->
            let cleanLine = line.TrimEnd('\r')
            let w = stringWidth cleanLine
            if w > limits.maxLineLength then
                Some {
                    line = idx + 1
                    column = 1
                    endLine = idx + 1
                    endColumn = cleanLine.Length + 1
                    severity = "warning"
                    message = $"Line exceeds 72 characters (width: {w})"
                }
            else None)
        |> Array.choose id

let checkFileLineCount (limits: StyleLimits) (content: string) : SyntaxDiagnostic[] =
    if System.String.IsNullOrEmpty content then [||]
    else
        let lines = content.Split('\n')
        let count = lines.Length
        if count > limits.maxFileLines then
            [| {
                line = 1
                column = 1
                endLine = 1
                endColumn = 1
                severity = "error"
                message = $"File exceeds 300 lines: must split (currently {count} lines)"
            } |]
        elif count > 200 then
            [| {
                line = 1
                column = 1
                endLine = 1
                endColumn = 1
                severity = "warning"
                message = $"File exceeds 200 lines: keep it simple (currently {count} lines)"
            } |]
        else [||]

let checkFunctionLengths (limits: StyleLimits) (nodes: AstNodeInfo[]) : SyntaxDiagnostic[] =
    nodes
    |> Array.filter (fun node -> limits.functionKinds.Contains node.kind)
    |> Array.choose (fun node ->
        let len = node.endLine - node.startLine + 1
        if len > limits.maxFunctionLines then
            Some {
                line = node.startLine
                column = 1
                endLine = node.endLine
                endColumn = 1
                severity = "warning"
                message = $"Function exceeds 50 lines (currently {len} lines)"
            }
        else None)

let hasSyntaxInOutput (text: string) : bool =
    match tryParse text with
    | Some msg -> msg.info |> List.exists (function InfoItem.Syntax _ -> true | _ -> false)
    | None -> false

/// Pure parser over a patchText blob: extracts every `*** Add File|Update
/// File|Move to: <path>` target, de-duplicated, order-preserved.
let private patchPathRe = Regex(@"^\*\*\* (?:Add File|Update File|Move to): (.+)$")

let pathsFromPatchText (patchText: string) : string list =
    if System.String.IsNullOrEmpty patchText then []
    else
        patchText.Split('\n')
        |> Seq.choose (fun line ->
            let m = patchPathRe.Match line
            if m.Success then Some m.Groups.[1].Value else None)
        |> List.ofSeq
        |> List.distinct

/// Format a syntax-check result into a human-readable annotation, or None when
/// the file is clean and `includeOk` is false.
let formatSyntaxDiagnostics (filePath: string) (result: SyntaxCheckResult)
                            (includeOk: bool) : string option =
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
                |> Array.map (fun e ->
                    $"  L{e.line}:{e.column}-{e.endLine}:{e.endColumn} [{e.severity}] {e.message}")
                |> Array.toList
            Some(String.concat "\n" (header :: body))

let appendSyntaxDiagnosticsToOutput (currentOutput: string) (filePath: string)
                                    (result: SyntaxCheckResult) : string =
    if hasSyntaxInOutput currentOutput then currentOutput
    else
        match formatSyntaxDiagnostics filePath result false with
        | Some formatted -> addSyntax currentOutput formatted
        | None -> currentOutput

let formatWriteSyntaxResult (filePath: string) (result: SyntaxCheckResult) : string =
    let syntaxText =
        match result with
        | Ok (_, [||]) -> ""
        | Ok (lang, errors) ->
            let header = $"Syntax check failed for {filePath} ({lang}):"
            let body =
                errors
                |> Array.map (fun e -> $"  line {e.line}, col {e.column}: {e.message}")
                |> String.concat "\n"
            header + "\n" + body
        | Failed (lang, reason) ->
            $"Syntax check failed for {filePath} ({lang}): {reason}"
    let successBody = $"Successfully wrote to {filePath}"
    match result with
    | Ok (_, [||]) -> render { empty with body = successBody }
    | _ -> addSyntax successBody syntaxText