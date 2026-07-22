module Wanxiangshu.Tests.TreeSitterKernelTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Runtime.TreeSitterFormat
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Runtime.ToolExecute
open Wanxiangshu.Runtime.TreeSitterShell
open Wanxiangshu.Runtime.Dyn

[<Import("createRequire", "node:module")>]
let private createRequire: string -> (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta: obj = jsNative

let private nodeReq: string -> obj = createRequire (string importMeta?url)
let private fsMod: obj = nodeReq "fs"

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string) (encoding: string) : string = jsNative

[<Import("readdirSync", "node:fs")>]
let private readdirSync (path: string) : string array = jsNative

[<Import("statSync", "node:fs")>]
let private statSyncRaw (path: string) : obj = jsNative

let private isDirectory (path: string) : bool =
    try
        let s = statSyncRaw path
        unbox<bool> (s?isDirectory ())
    with _ ->
        false

let rec private collectFsFilesSync (dir: string) : string list =
    try
        let entries = readdirSync dir

        entries
        |> Array.toList
        |> List.collect (fun name ->
            if
                name = "node_modules"
                || name = "build"
                || name = ".git"
                || name = "bin"
                || name = "obj"
            then
                []
            else
                let full = dir + "/" + name

                if isDirectory full then
                    collectFsFilesSync full
                elif name.EndsWith ".fs" || name.EndsWith ".fsi" then
                    [ full ]
                else
                    [])
    with _ ->
        []

/// Check a single .fs file via tree-sitter + direct line count, report violations.
let private checkSingleFile (filePath: string) (label: string) : JS.Promise<unit> =
    promise {
        let content =
            try
                readFileSync filePath "utf-8"
            with _ ->
                ""

        if content = "" then
            check (sprintf "%s: cannot read" label) false
        else
            let! syntaxResult = checkSyntax content filePath

            let allStyleDiags =
                match syntaxResult with
                | Ok(lang, diags) when lang <> "" ->
                    // tree-sitter parsed the file; style checks (file+function) are in diags
                    diags
                    |> Array.filter (fun d ->
                        d.message.Contains "File exceeds" || d.message.Contains "Function exceeds")
                | _ ->
                    // tree-sitter unavailable or can't detect language; run file line count directly
                    checkFileLineCount defaultStyleLimits content

            let errorDiags = allStyleDiags |> Array.filter (fun d -> d.severity = "error")

            if Array.isEmpty errorDiags then
                check (sprintf "%s: ok" label) true
            else
                for d in errorDiags do
                    check (sprintf "%s L%d: [%s] %s" label d.line d.severity d.message) false
    }

/// Generate per-file self-check test bodies (label + async function).
/// Each calls checkSyntax (tree-sitter) on one .fs file and reports
/// style violations (function length + file line count) as test failures.
let generateSelfCheckBodies () : (string * (unit -> JS.Promise<unit>)) list =
    collectFsFilesSync "src"
    |> List.sort
    |> List.map (fun filePath ->
        let label = "TreeSitterSelfCheck." + filePath
        label, (fun () -> checkSingleFile filePath label))

let isFileEditToolTrueForEdit () = check "edit" (isFileEditTool "edit")

let isFileEditToolTrueForWrite () = check "write" (isFileEditTool "write")

let isFileEditToolTrueForAstEdit () =
    check "ast_edit" (isFileEditTool "ast_edit")

let isFileEditToolFalseForRead () =
    check "not read" (not (isFileEditTool "read"))

let isFileEditToolFalseForExecutor () =
    check "not executor" (not (isFileEditTool "executor"))

let isFileEditToolCaseInsensitive () = check "Edit" (isFileEditTool "Edit")

let formatSyntaxDiagnosticsFailed () =
    let result =
        formatSyntaxDiagnostics "test.fs" (Failed("fsharp", "parse error")) true

    match result with
    | Some s -> check "contains failed" (s.Contains "Syntax check failed")
    | None -> failwith "expected Some"

let formatSyntaxDiagnosticsOkWithErrors () =
    let diags =
        [| { line = 5
             column = 3
             endLine = 5
             endColumn = 10
             severity = "error"
             message = "bad syntax" } |]

    let result = formatSyntaxDiagnostics "test.fs" (Ok("fsharp", diags)) true

    match result with
    | Some s -> check "contains error count" (s.Contains "1 syntax issue")
    | None -> failwith "expected Some"

let formatSyntaxDiagnosticsOkCleanExclude () =
    let result = formatSyntaxDiagnostics "test.fs" (Ok("fsharp", [||])) false
    equal "none" None result

let formatSyntaxDiagnosticsOkCleanInclude () =
    let result = formatSyntaxDiagnostics "test.fs" (Ok("fsharp", [||])) true

    match result with
    | Some s -> check "ok" (s.Contains "ok")
    | None -> failwith "expected Some"

let hasSyntaxInOutputWithSyntax () =
    let output = addSyntax (plainText "base") "test syntax" |> render
    check "has syntax" (hasSyntaxInOutput output)

let hasSyntaxInOutputWithoutSyntax () =
    check "no syntax" (not (hasSyntaxInOutput "plain output"))

let appendSyntaxDiagnosticsToOutputNoExisting () =
    let result =
        appendSyntaxDiagnosticsToOutput "base" "test.fs" (Failed("fsharp", "error"))

    check "append" (result <> "base")

let appendSyntaxDiagnosticsToOutputExistingKeeps () =
    let existing = addSyntax (plainText "base") "syntax here" |> render

    let result =
        appendSyntaxDiagnosticsToOutput existing "test.fs" (Failed("fsharp", "err"))

    check "existing keeps content" (result.Contains "base" && result.Contains "syntax here")

let formatWriteSyntaxResultClean () =
    let result = formatWriteSyntaxResult "test.fs" (Ok("fsharp", [||]))
    check "clean" (result.Contains "success = true")

let formatWriteSyntaxResultFailed () =
    let result = formatWriteSyntaxResult "test.fs" (Failed("fsharp", "parse error"))
    check "error msg" (result.Contains "Syntax check failed")

let formatWriteSyntaxResultErrors () =
    let diags =
        [| { line = 1
             column = 0
             endLine = 1
             endColumn = 5
             severity = "error"
             message = "syntax err. Do not compress code to bypass length limits; you must split functions." } |]

    let result = formatWriteSyntaxResult "test.fs" (Ok("fsharp", diags))
    check "error detail" (result.Contains "line 1, col 0:")

    check
        "format contains suggest"
        (result.Contains "Do not compress code to bypass length limits; you must split functions.")

let testCheckFunctionLengthIsError () =
    let limits = defaultStyleLimits

    let nodes =
        [| { kind = "function_definition"
             startLine = 1
             endLine = 52 } |]

    let diags = checkFunctionLengths limits nodes
    equal "diags length" 1 diags.Length
    equal "severity warning" "warning" diags.[0].severity

    check
        "message contains suggest"
        (diags.[0].message.Contains "Do not compress code to bypass length limits; you must split functions.")

let testCheckFileLineCount () =
    let limits = defaultStyleLimits
    let lines200 = String.concat "\n" (Array.create 200 "a")
    let lines201 = String.concat "\n" (Array.create 201 "a")
    let lines300 = String.concat "\n" (Array.create 300 "a")
    let lines301 = String.concat "\n" (Array.create 301 "a")
    equal "200 lines" 0 (checkFileLineCount limits lines200).Length
    let diags201 = checkFileLineCount limits lines201
    equal "201 lines count" 1 diags201.Length
    equal "201 lines severity" "warning" diags201.[0].severity
    check "201 lines msg" (diags201.[0].message.Contains "200 lines")

    check
        "201 lines suggest"
        (diags201.[0].message.Contains "Do not compress code to bypass length limits; you must split files.")

    let diags300 = checkFileLineCount limits lines300
    equal "300 lines count" 1 diags300.Length
    equal "300 lines severity" "warning" diags300.[0].severity
    let diags301 = checkFileLineCount limits lines301
    equal "301 lines count" 1 diags301.Length
    equal "301 lines severity" "error" diags301.[0].severity
    check "301 lines msg" (diags301.[0].message.Contains "300 lines")

    check
        "301 lines suggest"
        (diags301.[0].message.Contains "Do not compress code to bypass length limits; you must split files.")

let testCheckFunctionLengths () =
    let limits = defaultStyleLimits

    let nodes =
        [| { kind = "function_definition"
             startLine = 10
             endLine = 60 }
           { kind = "function_definition"
             startLine = 70
             endLine = 119 }
           { kind = "let_binding"
             startLine = 130
             endLine = 181 }
           { kind = "other_node"
             startLine = 200
             endLine = 300 } |]

    let diags = checkFunctionLengths limits nodes
    equal "function diags count" 2 diags.Length
    equal "diag 1 line" 10 diags.[0].line
    equal "diag 2 line" 130 diags.[1].line

    check
        "diag 1 msg"
        (diags.[0].message.Contains "Do not compress code to bypass length limits; you must split functions.")

    check
        "diag 2 msg"
        (diags.[1].message.Contains "Do not compress code to bypass length limits; you must split functions.")

    equal "diag 1 severity" "warning" diags.[0].severity
    equal "diag 2 severity" "warning" diags.[1].severity

let testCheckFunctionLengthsErrorThreshold () =
    let limits = defaultStyleLimits

    let nodes =
        [| { kind = "function_definition"
             startLine = 1
             endLine = 61 } |]

    let diags = checkFunctionLengths limits nodes
    equal "error diags length" 1 diags.Length
    equal "severity error" "error" diags.[0].severity
    check "error message" (diags.[0].message.Contains "Function exceeds 60 lines")

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
    testCheckFunctionLengthIsError ()
    testCheckFileLineCount ()
    testCheckFunctionLengths ()
    testCheckFunctionLengthsErrorThreshold ()
