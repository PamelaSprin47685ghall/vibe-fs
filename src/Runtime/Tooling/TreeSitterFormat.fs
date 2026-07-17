module Wanxiangshu.Runtime.TreeSitterFormat

open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Runtime.ToolOutputInfo

let hasSyntaxInOutput (text: string) : bool =
    match tryParse text with
    | Some msg ->
        msg.info
        |> List.exists (function
            | InfoItem.Syntax _ -> true
            | _ -> false)
    | None -> false

let appendSyntaxDiagnosticsToOutput (currentOutput: string) (filePath: string) (result: SyntaxCheckResult) : string =
    if hasSyntaxInOutput currentOutput then
        currentOutput
    else
        match formatSyntaxDiagnostics filePath result false with
        | Some formatted -> addSyntax currentOutput formatted
        | None -> currentOutput

let formatWriteSyntaxResult (filePath: string) (result: SyntaxCheckResult) : string =
    let syntaxText =
        match result with
        | Ok(_, [||]) -> ""
        | Ok(lang, errors) ->
            let header = $"Syntax check failed for {filePath} ({lang}):"

            let body =
                errors
                |> Array.map (fun e -> $"  line {e.line}, col {e.column}: {e.message}")
                |> String.concat "\n"

            header + "\n" + body
        | Failed(lang, reason) -> $"Syntax check failed for {filePath} ({lang}): {reason}"

    let successBody = $"Successfully wrote to {filePath}"

    match result with
    | Ok(_, [||]) -> render { empty with body = successBody }
    | _ -> addSyntax successBody syntaxText
