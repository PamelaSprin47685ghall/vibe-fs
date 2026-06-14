module VibeFs.Shell.TreeSitterShell

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.SyntaxTypes
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Kernel

/// Run the tree-sitter pack end-to-end via a separate JS helper that is easier
/// to audit and test than a giant inline Emit string.
[<Import("default", "./TreeSitterSyntax.mjs")>]
let private checkSyntaxRaw (content: string) (filePath: string) : JS.Promise<obj> = jsNative

[<Emit("import('node:fs/promises')")>]
let private importFsPromises () : JS.Promise<obj> = jsNative

[<Emit("require('node:path').resolve($0, $1)")>]
let private resolvePath (cwd: string) (filePath: string) : string = jsNative

/// Check a file's syntax, mapping the raw JS result into the typed DU.
let checkSyntax (content: string) (filePath: string) : JS.Promise<SyntaxCheckResult> =
    async {
        let! raw = checkSyntaxRaw content filePath |> Async.AwaitPromise
        let ok = Dyn.getValue<bool> raw "ok"
        if not ok then
            return Failed(Dyn.str raw "lang", Dyn.str raw "reason")
        else
            let lang = Dyn.str raw "lang"
            let errorsArr = Dyn.getValue<obj array> raw "errors"
            let errors =
                [ for e in errorsArr do
                    { line = Dyn.getValue<int> e "line"
                      column = Dyn.getValue<int> e "column"
                      endLine = Dyn.getValue<int> e "endLine"
                      endColumn = Dyn.getValue<int> e "endColumn"
                      severity = Dyn.str e "severity"
                      message = Dyn.str e "message" } ]
            return Ok(lang, errors)
    }
    |> Async.StartAsPromise

/// Read a file relative to cwd, check its syntax, and format diagnostics.
let readAndCheckSyntax (filePath: string) (cwd: string) (includeOk: bool) : JS.Promise<string option> =
    async {
        try
            let! api = importFsPromises () |> Async.AwaitPromise
            let abs = resolvePath cwd filePath
            let! content = (Dyn.call2 (Dyn.get api "readFile") abs "utf-8" :?> JS.Promise<string>) |> Async.AwaitPromise
            let! result = checkSyntax content filePath |> Async.AwaitPromise
            return formatSyntaxDiagnostics filePath result includeOk
        with _ -> return None
    }
    |> Async.StartAsPromise

let appendSyntaxDiagnostics (filePath: string) (content: string) (includeOk: bool)
                            : JS.Promise<string option> =
    async {
        let! result = checkSyntax content filePath |> Async.AwaitPromise
        return formatSyntaxDiagnostics filePath result includeOk
    }
    |> Async.StartAsPromise
