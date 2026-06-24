module VibeFs.Kernel.TreeSitterKernel

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

let syntaxCheckMarker = "[syntax-check]"

let hasSyntaxCheckMarker (text: string) : bool = text.Contains(syntaxCheckMarker)

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