module VibeFs.Shell.TreeSitterShell

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.SyntaxTypes
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Shell.Path
open VibeFs.Shell.TreeSitterSyntax

[<Emit("import('node:fs/promises')")>]
let private importFsPromises () : JS.Promise<obj> = jsNative

let checkSyntax (content: string) (filePath: string) : JS.Promise<SyntaxCheckResult> =
    TreeSitterSyntax.checkSyntax content filePath

let readAndCheckSyntax (filePath: string) (cwd: string) (includeOk: bool) : JS.Promise<string option> =
    async {
        try
            let! api = importFsPromises () |> Async.AwaitPromise
            let abs = resolve cwd filePath
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
