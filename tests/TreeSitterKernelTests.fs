module Wanxiangshu.Tests.TreeSitterKernelTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Kernel.ToolOutputInfo

let isFileEditToolTrueForEdit () =
    check "edit" (isFileEditTool "edit")

let isFileEditToolTrueForWrite () =
    check "write" (isFileEditTool "write")

let isFileEditToolTrueForAstEdit () =
    check "ast_edit" (isFileEditTool "ast_edit")

let isFileEditToolFalseForRead () =
    check "not read" (not (isFileEditTool "read"))

let isFileEditToolFalseForExecutor () =
    check "not executor" (not (isFileEditTool "executor"))

let isFileEditToolCaseInsensitive () =
    check "Edit" (isFileEditTool "Edit")

let formatSyntaxDiagnosticsFailed () =
    let result = formatSyntaxDiagnostics "test.fs" (Failed ("fsharp", "parse error")) true
    match result with
    | Some s -> check "contains failed" (s.Contains "Syntax check failed")
    | None -> failwith "expected Some"

let formatSyntaxDiagnosticsOkWithErrors () =
    let diags = [| { line = 5; column = 3; endLine = 5; endColumn = 10; severity = "error"; message = "bad syntax" } |]
    let result = formatSyntaxDiagnostics "test.fs" (Ok ("fsharp", diags)) true
    match result with
    | Some s -> check "contains error count" (s.Contains "1 syntax issue")
    | None -> failwith "expected Some"

let formatSyntaxDiagnosticsOkCleanExclude () =
    let result = formatSyntaxDiagnostics "test.fs" (Ok ("fsharp", [||])) false
    equal "none" None result

let formatSyntaxDiagnosticsOkCleanInclude () =
    let result = formatSyntaxDiagnostics "test.fs" (Ok ("fsharp", [||])) true
    match result with
    | Some s -> check "ok" (s.Contains "ok")
    | None -> failwith "expected Some"

let hasSyntaxInOutputWithSyntax () =
    let output = addSyntax "base" "test syntax"
    check "has syntax" (hasSyntaxInOutput output)

let hasSyntaxInOutputWithoutSyntax () =
    check "no syntax" (not (hasSyntaxInOutput "plain output"))

let appendSyntaxDiagnosticsToOutputNoExisting () =
    let result = appendSyntaxDiagnosticsToOutput "base" "test.fs" (Failed ("fsharp", "error"))
    check "append" (result <> "base")

let appendSyntaxDiagnosticsToOutputExistingKeeps () =
    let existing = addSyntax "base" "syntax here"
    let result = appendSyntaxDiagnosticsToOutput existing "test.fs" (Failed ("fsharp", "err"))
    equal "unchanged" existing result

let formatWriteSyntaxResultClean () =
    let result = formatWriteSyntaxResult "test.fs" (Ok ("fsharp", [||]))
    check "clean" (result.Contains "Successfully wrote")

let formatWriteSyntaxResultFailed () =
    let result = formatWriteSyntaxResult "test.fs" (Failed ("fsharp", "parse error"))
    check "error msg" (result.Contains "Syntax check failed")

let formatWriteSyntaxResultErrors () =
    let diags = [| { line = 1; column = 0; endLine = 1; endColumn = 5; severity = "error"; message = "syntax err" } |]
    let result = formatWriteSyntaxResult "test.fs" (Ok ("fsharp", diags))
    check "error detail" (result.Contains "line 1, col 0:")

let run () =
    isFileEditToolTrueForEdit ()
    isFileEditToolTrueForWrite ()
    isFileEditToolTrueForAstEdit ()
    isFileEditToolFalseForRead ()
    isFileEditToolFalseForExecutor ()
    isFileEditToolCaseInsensitive ()
    formatSyntaxDiagnosticsFailed ()
    formatSyntaxDiagnosticsOkWithErrors ()
    formatSyntaxDiagnosticsOkCleanExclude ()
    formatSyntaxDiagnosticsOkCleanInclude ()
    hasSyntaxInOutputWithSyntax ()
    hasSyntaxInOutputWithoutSyntax ()
    appendSyntaxDiagnosticsToOutputNoExisting ()
    appendSyntaxDiagnosticsToOutputExistingKeeps ()
    formatWriteSyntaxResultClean ()
    formatWriteSyntaxResultFailed ()
    formatWriteSyntaxResultErrors ()
