module VibeFs.Kernel.TreeSitterKernel

open System.Text.RegularExpressions
open VibeFs.Kernel

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

let syntaxCheckMarker = "[syntax-check]"

let isFileEditTool (tool: string) : bool = Set.contains (tool.ToLowerInvariant ()) fileEditTools

/// Pure parser over a patchText blob: extracts every `*** Add File|Update
/// File|Move to: <path>` target, de-duplicated, order-preserved.  Lives on its
/// own so `extractFilePaths` no longer mixes "pull a key off args" with "scan a
/// patch body" — the two concerns have nothing in common (P38/P39).
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

/// Pull file path(s) out of a tool call's args — the args-side concern only:
/// resolve `path` / `file_path` / `filePath`; when absent, hand the patchText
/// body to the dedicated `pathsFromPatchText` parser.
let extractFilePaths (args: obj) : string list =
    if Dyn.isNullish args then []
    else
        let path =
            [ "path"; "file_path"; "filePath" ]
            |> List.tryPick (fun key ->
                let value = Dyn.get args key
                if Dyn.isNullish value then None else Some(string value))
        match path with
        | Some p when p <> "" -> [ p ]
        | _ ->
            let patchText = Dyn.get args "patchText"
            if Dyn.isNullish patchText then []
            else pathsFromPatchText (string patchText)

let extractFilePath (args: obj) : string option =
    extractFilePaths args |> List.tryHead

let hasSyntaxCheckMarker (text: string) : bool = text.Contains(syntaxCheckMarker)

/// Format a syntax-check result into a human-readable annotation, or None when
/// the file is clean and `includeOk` is false.
let formatSyntaxDiagnostics (filePath: string) (result: SyntaxCheckResult)
                            (includeOk: bool) : string option =
    match result with
    | Failed(lang, reason) ->
        let langLabel = if lang = "" then "unknown" else lang
        Some($"{syntaxCheckMarker}\nSyntax check failed in {filePath} ({langLabel}): {reason}")
    | Ok(lang, errors) ->
        if Array.isEmpty errors then
            if includeOk then Some($"{syntaxCheckMarker} {filePath}: ok ({lang})") else None
        else
            let header = $"{syntaxCheckMarker}\n{errors.Length} syntax issue(s) in {filePath} ({lang})"
            let body =
                errors
                |> Array.map (fun e ->
                    $"  L{e.line}:{e.column}-{e.endLine}:{e.endColumn} [{e.severity}] {e.message}")
                |> Array.toList
            Some(String.concat "\n" (header :: body))

/// Append a syntax annotation to tool output, skipping when already marked.
let appendSyntaxDiagnosticsToOutput (currentOutput: string) (filePath: string)
                                    (result: SyntaxCheckResult) : string =
    if hasSyntaxCheckMarker currentOutput then currentOutput
    else
        match formatSyntaxDiagnostics filePath result false with
        | Some formatted -> $"{currentOutput}\n\n{formatted}"
        | None -> currentOutput

let formatWriteSyntaxResult (filePath: string) (result: SyntaxCheckResult) : string =
    match result with
    | Ok (_, [||]) ->
        $"Successfully wrote to {filePath}"
    | Ok (lang, errors) ->
        let header = $"[syntax-check] Syntax check failed for {filePath} ({lang}):"
        let body =
            errors
            |> Array.map (fun e -> $"  line {e.line}, col {e.column}: {e.message}")
            |> String.concat "\n"
        header + "\n" + body
    | Failed (lang, reason) ->
        $"[syntax-check] Syntax check failed for {filePath} ({lang}): {reason}"
