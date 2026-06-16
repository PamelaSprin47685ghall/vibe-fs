module VibeFs.Kernel.TreeSitterKernel

open System.Text.RegularExpressions
open VibeFs.Kernel.SyntaxTypes
open VibeFs.Kernel

/// Tools whose results should be syntax-checked after a write.
let private fileEditTools: Set<string> =
    Set.ofList
        [ "edit"; "write"; "ast_edit"; "ast_grep_replace"; "file_edit_replace_string"
          "file_edit_insert"; "apply_patch" ]

let syntaxCheckMarker = "[syntax-check]"

let isFileEditTool (tool: string) : bool = Set.contains (tool.ToLowerInvariant ()) fileEditTools

/// Pull file path(s) out of a tool call's args — supports path/file_path/filePath,
/// or extracts `*** Add/Update/Move to: <path>` lines from patchText.
let extractFilePaths (args: obj) : string list =
    if Dyn.isNullish args then []
    else
        match MessageDecoder.firstPresent [ "path"; "file_path"; "filePath" ] args with
        | Some path when path <> "" -> [ path ]
        | _ ->
            let patchText = Dyn.get args "patchText"
            if not (Dyn.isNullish patchText) && string patchText <> "" then
                (string patchText).Split('\n')
                |> Seq.choose (fun line ->
                    let m = Regex.Match(line, @"^\*\*\* (?:Add File|Update File|Move to): (.+)$")
                    if m.Success then Some m.Groups.[1].Value else None)
                |> List.ofSeq |> List.distinct
            else []

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
            let lines =
                errors |> Array.map (fun e ->
                    $"  L{e.line}:{e.column}-{e.endLine}:{e.endColumn} [{e.severity}] {e.message}")
            Some(String.concat "\n" ($"{syntaxCheckMarker}\n{errors.Length} syntax issue(s) in {filePath} ({lang})" :: (lines |> Array.toList)))

/// Append a syntax annotation to tool output, skipping when already marked.
let appendSyntaxDiagnosticsToOutput (currentOutput: string) (filePath: string)
                                    (result: SyntaxCheckResult) : string =
    if hasSyntaxCheckMarker currentOutput then currentOutput
    else
        match formatSyntaxDiagnostics filePath result false with
        | Some formatted -> $"{currentOutput}\n\n{formatted}"
        | None -> currentOutput
