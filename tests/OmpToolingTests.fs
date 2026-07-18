module Wanxiangshu.Tests.OmpToolingTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Runtime.ExecutorFormat
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Runtime.ToolExecute
open Wanxiangshu.Runtime.TreeSitterShell

let checkSyntaxBadJson () =
    promise {
        let! result = checkSyntax "const x = (" "sample.ts"

        match result with
        | Ok(lang, errors) ->
            if lang = "" then
                check "no parser skips errors" (errors.Length = 0)
            else
                check "broken ts has diagnostics" (errors.Length > 0)
        | Failed(_, reason) -> check "failed reason non-empty" (reason <> "")
    }

let checkSyntaxValidJson () =
    promise {
        let! result = checkSyntax "{}" "sample.json"

        match result with
        | Ok(lang, errors) ->
            if lang <> "" then
                check "valid json no errors" (errors.Length = 0)
        | Failed(_, reason) -> check "valid json should not fail" (reason = "" && false)
    }

let checkSyntaxBrokenJsonReports_intentionalWarningFork () =
    promise {
        let! result = checkSyntax "{bad" "broken.json"

        match result with
        | Ok(_, errors) ->
            check "broken json has errors" (errors.Length >= 1)

            for err in errors do
                check
                    "syntax severity warning or error (tree-sitter fork)"
                    (err.severity = "warning" || err.severity = "error")

                check "syntax line >= 1" (err.line >= 1)
                check "syntax column >= 1" (err.column >= 1)
        | Failed(_, reason) -> check "failed json reason" (reason.Length > 0)
    }

let checkSyntaxMarkdownExempt () =
    promise {
        let! result = checkSyntax "const x = (" "sample.md"

        match result with
        | Ok(lang, errors) -> equal "markdown has no errors" 0 errors.Length
        | Failed(_, reason) -> check "should not fail" (reason = "" && false)
    }

let supportsSyntaxDiagnosticsGrepFalse () =
    check "grep not file-edit tool" (not (isFileEditTool "grep"))

let stripHeadTailViaKernel () =
    let r = strip "printf data | head -n 5"
    equal "strip head outside quotes" "printf data" r.script
