module Wanxiangshu.Tests.OmpHelpersTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Omp.Plugin
open Wanxiangshu.Shell.TreeSitterShell
open Wanxiangshu.Shell.Dyn

[<Global("process")>]
let private nodeProcess : obj = jsNative

let private resetTest () =
    emitJsExpr _test "$0.reset()"
    |> ignore

let checkSyntaxBadJson () = promise {
    let! result = checkSyntax "const x = (" "sample.ts"
    match result with
    | Ok(lang, errors) ->
        if lang = "" then check "no parser skips errors" (errors.Length = 0)
        else check "broken ts has diagnostics" (errors.Length > 0)
    | Failed(_, reason) ->
        check "failed reason non-empty" (reason <> "")
}

let checkSyntaxValidJson () = promise {
    let! result = checkSyntax "{}" "sample.json"
    match result with
    | Ok(lang, errors) ->
        if lang <> "" then check "valid json no errors" (errors.Length = 0)
    | Failed(_, reason) -> check "valid json should not fail" (reason = "" && false)
}

let checkSyntaxBrokenJsonReports_intentionalWarningFork () = promise {
    // vibe-omp plugin.test.ts: Ok path requires severity === 'error'; TreeSitterShell.collectDiagnostics fixes severity = "warning".
    let! result = checkSyntax "{bad" "broken.json"
    match result with
    | Ok(_, errors) ->
        check "broken json has errors" (errors.Length >= 1)
        for err in errors do
            check "syntax severity warning or error (tree-sitter fork)" (err.severity = "warning" || err.severity = "error")
            check "syntax line >= 1" (err.line >= 1)
            check "syntax column >= 1" (err.column >= 1)
    | Failed(_, reason) ->
        check "failed json reason" (reason.Length > 0)
}

let supportsSyntaxDiagnosticsFileEditTools () = promise {
    let! w =
        emitJsExpr (_test, "Write")
            "Promise.resolve($0.supportsSyntaxDiagnosticsTool($1))"
        |> unbox<JS.Promise<bool>>
    let! a =
        emitJsExpr (_test, "ast_grep_replace")
            "Promise.resolve($0.supportsSyntaxDiagnosticsTool($1))"
        |> unbox<JS.Promise<bool>>
    let! e =
        emitJsExpr (_test, "edit")
            "Promise.resolve($0.supportsSyntaxDiagnosticsTool($1))"
        |> unbox<JS.Promise<bool>>
    let! g =
        emitJsExpr (_test, "grep")
            "Promise.resolve($0.supportsSyntaxDiagnosticsTool($1))"
        |> unbox<JS.Promise<bool>>
    check "Write is file-edit" w
    check "ast_grep_replace is file-edit" a
    check "edit is file-edit" e
    check "grep not file-edit" (not g)
}

let supportsSyntaxDiagnosticsGrepFalse () =
    check "grep not file-edit tool" (not (isFileEditTool "grep"))

let stripHeadTailViaKernel () =
    let r = strip "printf data | head -n 5"
    equal "strip head outside quotes" "printf data" r.script

let stripHeadTailChain () =
    let script =
        emitJsExpr (_test, "cat a | head -n 20 | tail -5")
            "$0.stripHeadTailPipes($1).script"
        |> unbox<string>
    equal "strip head and tail chain" "cat a" script

let getOllamaApiKeyFromEnv () =
    let env = nodeProcess?env
    let previous = env?("OLLAMA_API_KEY")
    env?("OLLAMA_API_KEY") <- "test-ollama-key"
    try
        equal "ollama key from env" "test-ollama-key" (_test?getOllamaKey() |> unbox<string>)
    finally
        if isNullish previous then
            emitJsExpr env "delete $0.OLLAMA_API_KEY"
        else
            env?("OLLAMA_API_KEY") <- previous

let getOllamaApiKeyMissingWhenUnset () =
    let env = nodeProcess?env
    let previous = env?("OLLAMA_API_KEY")
    emitJsExpr env "delete $0.OLLAMA_API_KEY"
    try
        equal "ollama key missing when unset" "" (_test?getOllamaKey() |> unbox<string>)
    finally
        if isNullish previous then
            emitJsExpr env "delete $0.OLLAMA_API_KEY"
        else
            env?("OLLAMA_API_KEY") <- previous

// OMP production fuzzy tools scope iterators by sessionId (fallback workspaceId) in FuzzyTools.scopeId.
// _test.fuzzy store/consume uses OmpTestHooks.fuzzyScope = "global" so harness tests avoid a live Pi session id.
let fuzzyGrepCursorSingleUse () =
    resetTest ()
    let firstId =
        emitJsExpr _test
            """$0.fuzzy.storeCursor({ externalBasePath: '/tmp/demo', query: 'x', cursor: { token: 1 } })"""
        |> unbox<string>
    let consumed =
        emitJsExpr (_test, firstId)
            """$0.fuzzy.consumeCursor($1)"""
        |> unbox<obj>
    equal "grep cursor external path" "/tmp/demo" (str consumed "externalBasePath")
    let second =
        emitJsExpr (_test, firstId)
            """$0.fuzzy.consumeCursor($1)"""
        |> unbox<obj>
    check "grep cursor single-use" (isNullish second)

let fuzzyFindCursorSingleUse () =
    resetTest ()
    let firstId =
        emitJsExpr _test
            """$0.fuzzy.storeFindCursor({ query: 'src main', pageSize: 30, pageIndex: 1 })"""
        |> unbox<string>
    let consumed =
        emitJsExpr (_test, firstId)
            """$0.fuzzy.consumeFindCursor($1)"""
        |> unbox<obj>
    equal "find cursor pageIndex" 1 (getValue<int> consumed "pageIndex")
    let second =
        emitJsExpr (_test, firstId)
            """$0.fuzzy.consumeFindCursor($1)"""
        |> unbox<obj>
    check "find cursor single-use" (isNullish second)

let fuzzyResolveExternalBasePath () =
    resetTest ()
    let resolved =
        emitJsExpr _test
            """$0.fuzzy.resolveExternalBasePath('/tmp/demo/file.ts')"""
        |> unbox<obj>
    equal "resolve basePath" "/tmp/demo" (str resolved "basePath")
    equal "resolve pathConstraint" "file.ts" (str resolved "pathConstraint")