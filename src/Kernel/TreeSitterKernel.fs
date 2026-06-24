module VibeFs.Kernel.TreeSitterKernel

open VibeFs.Kernel.ToolOutputInfo

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

/// Tools whose results should be syntax-checked after a write.
let private fileEditTools: Set<string> =
    Set.ofList
        [ "edit"; "write"; "ast_edit"; "ast_grep_replace"; "file_edit_replace_string"
          "file_edit_insert"; "apply_patch" ]

let isFileEditTool (tool: string) : bool = Set.contains (tool.ToLowerInvariant ()) fileEditTools

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

/// Append a syntax annotation to tool output, skipping when already marked.
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
    | Ok (_, [||]) -> seeBelowEnvelope successBody
    | _ -> addSyntax successBody syntaxText
