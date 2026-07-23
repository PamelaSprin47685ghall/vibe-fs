module Wanxiangshu.Tests.ExecutorSpawnPathTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.ExecutorSpawn
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Runtime.ExecutorFormat
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Runtime.SearchPrompts
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.Dyn

[<Import("createRequire", "node:module")>]
let private createRequire': string -> (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta: obj = jsNative

let private requireFn: string -> obj = createRequire' (string importMeta?url)
let private pathModule: obj = requireFn "path"










let executorMapping () =
    let opts: ExecuteOptions =
        { command = "echo x"
          language = Shell
          dependencies = []
          timeoutType = Long
          cwd = None
          whatToSummarize = ""
          maxBytes = 8192 }

    let run o =
        Wanxiangshu.Runtime.Executor.mapOutcome opts 10000 "out" "err" o

    check
        "exit0→Completed"
        (match run (Exited(0, "out", "err")) with
         | Completed(stdout, stderr, 0) when stdout = "out" && stderr = "err" -> true
         | _ -> false)

    check
        "nonzero→Failed"
        (match run (Exited(2, "out", "err")) with
         | Failed(stdout, stderr, Some 2, None) when stdout = "out" && stderr = "err" -> true
         | _ -> false)

    check
        "timeout→Truncated"
        (match run (TimedOut("out", "err")) with
         | Truncated _ -> true
         | _ -> false)

    check
        "signaled→Failed"
        (match run (Signaled("SIGKILL", "out", "err")) with
         | Failed(_, _, None, Some "SIGKILL") -> true
         | _ -> false)

    check
        "spawnFail→MissingExecutable"
        (match run (SpawnFailed(Wanxiangshu.Kernel.Errors.DomainError.ExecutorExecutableMissing "bash")) with
         | MissingExecutable("bash", _) -> true
         | _ -> false)

    check
        "spawnFail(other)→Failed"
        (match run (SpawnFailed(Wanxiangshu.Kernel.Errors.DomainError.SystemPanic "boom")) with
         | Failed _ -> true
         | _ -> false)

    equal "python exe uvx" "uvx" (Wanxiangshu.Runtime.Executor.missingExecutableFor Python)

let capsFileShape () =
    let f: Wanxiangshu.Runtime.CapsFormat.CapsFile =
        { filePath = "/abs/HERE.md"
          label = "HERE.md"
          content = "x" }

    equal "capsFile filePath" "/abs/HERE.md" f.filePath
    equal "capsFile label" "HERE.md" f.label

let capsFileSizeLimit () =
    equal "caps file size limit 4MB" (4 * 1_048_576) Wanxiangshu.Runtime.WorkspaceFiles.maxFileSize

let stripHeadTailPipesOutsideQuotes () =
    let r = strip "cat f | head -n 5"
    equal "strip head -n 5 outside quotes" "cat f" r.script

let stripHeadTailPipesHeadTailChain () =
    let r = strip "cat a | head -n 20 | tail -5"
    equal "strip head then tail chain" "cat a" r.script

let readDirectoryListing () =
    promise {
        let! workspaceDir = mkdtempAsync "read-dir-"
        let nestedDir = unbox<string> (pathModule?join (workspaceDir, "nested"))
        let filePath = unbox<string> (pathModule?join (workspaceDir, "note.txt"))
        do! writeFileAsync filePath "hello"
        let fsAsync: obj = get (requireFn "fs") "promises"
        do! unbox<JS.Promise<unit>> (fsAsync?mkdir (nestedDir))
        let! listing = Wanxiangshu.Runtime.FileSys.read None workspaceDir None None
        check "directory listing contains file" (listing.Contains "note.txt")
        check "directory listing contains directory" (listing.Contains "nested")
        check "directory listing has total header" (listing.Contains "total 2")
        do! rmAsync workspaceDir
    }

let ensureJavascriptProjectRepairsModuleType () =
    promise {
        let! projectDir = mkdtempAsync "executor-js-project-"
        let packageJsonPath = unbox<string> (pathModule?join (projectDir, "package.json"))
        do! writeFileAsync packageJsonPath "{\n  \"dependencies\": {\n    \"tsx\": \"*\"\n  }\n}\n"
        let scope = RuntimeScope()
        let! _ = Wanxiangshu.Runtime.ExecutorJavascript.ensureJavascriptProject scope projectDir [] None None
        let fsAsync: obj = get (requireFn "fs") "promises"
        let! packageJson = unbox<JS.Promise<string>> (fsAsync?readFile (packageJsonPath, "utf-8"))
        check "ensureJavascriptProject writes type module" (packageJson.Contains "\"type\": \"module\"")
        do! rmAsync projectDir
    }

let private canRequireEsModuleLexer () : bool =
    try
        requireFn "es-module-lexer" |> ignore
        true
    with _ ->
        false

let rewriteJavascriptRelativeImports () =
    promise {
        if not (canRequireEsModuleLexer ()) then
            ()
        else
            let program = "import { x } from \"./foo.js\";\nconsole.log(x);\n"
            let! rewritten = Wanxiangshu.Runtime.ExecutorJavascript.rewriteJavascriptModuleSpecifiers program "/abs/cwd"
            check "relative import rewritten to file URL" (rewritten.Contains "file:///")
            check "relative specifier consumed" (not (rewritten.Contains "\"./foo.js\""))
            check "non-relative body preserved" (rewritten.Contains "console.log(x)")
    }
