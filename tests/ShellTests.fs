module Wanxiangshu.Tests.ShellTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.ExecutorSpawn
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Kernel.SubagentPrompts
open Wanxiangshu.Kernel.SearchPrompts
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn

[<Import("createRequire", "node:module")>]
let private createRequire' : string -> (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta : obj = jsNative

let private requireFn : string -> obj = createRequire'(string importMeta?url)
let private pathModule : obj = requireFn("path")

let webApiFetchInit () =
    let init = Wanxiangshu.Shell.WebSearchApi.postInit "KEY123" "{\"a\":1}" None
    equal "init method POST" "POST" (Wanxiangshu.Shell.Dyn.str init "method")
    let headers = Wanxiangshu.Shell.Dyn.get init "headers"
    equal "init Content-Type" "application/json" (Wanxiangshu.Shell.Dyn.str headers "Content-Type")
    let auth = Wanxiangshu.Shell.Dyn.str headers "Authorization"
    check "init Authorization key" (auth.Contains "KEY123")
    equal "init body json" "{\"a\":1}" (Wanxiangshu.Shell.Dyn.str init "body")
    check "init no signal when None" (Wanxiangshu.Shell.Dyn.isNullish (Wanxiangshu.Shell.Dyn.get init "signal"))
    let withSignal = Wanxiangshu.Shell.WebSearchApi.postInit "K" "b" (Some (box "ABORT"))
    check "init signal when Some" (not (Wanxiangshu.Shell.Dyn.isNullish (Wanxiangshu.Shell.Dyn.get withSignal "signal")))

let webApiResponseMethodCall () =
    let response =
        createObj
            [ "text", box (fun () -> "body")
              "json", box (fun () -> createObj [ "ok", box "yes" ]) ]
    equal "response.text() invoked" "body" (unbox<string> (Wanxiangshu.Shell.WebSearchApi.responseMethod0 response "text"))
    let json = Wanxiangshu.Shell.WebSearchApi.responseMethod0 response "json"
    equal "response.json() invoked" "yes" (Wanxiangshu.Shell.Dyn.str json "ok")

let webApiKeyValidation () =
    equal "requireWebApiKey trims" (Ok "KEY123") (Wanxiangshu.Shell.WebSearchApi.requireWebApiKey "  KEY123  ")
    equal "requireWebApiKey rejects missing" (Error "Missing OLLAMA_API_KEY environment variable.") (Wanxiangshu.Shell.WebSearchApi.requireWebApiKey "")
    equal "requireWebApiKey rejects empty" (Error "Missing OLLAMA_API_KEY environment variable.") (Wanxiangshu.Shell.WebSearchApi.requireWebApiKey "   ")

let executorMapping () =
    let opts : ExecuteOptions =
        { program = "echo x"; language = Shell; dependencies = []; timeoutType = Long; mode = "ro"; cwd = None; whatToSummarize = "" }
    let run o = Wanxiangshu.Shell.Executor.mapOutcome opts 10000 "out" o
    check "exit0→Completed" (match run (Exited(0, "", "")) with Completed _ -> true | _ -> false)
    check "nonzero→Failed" (match run (Exited(2, "", "")) with Failed _ -> true | _ -> false)
    check "timeout→Truncated" (match run (TimedOut("", "")) with Truncated _ -> true | _ -> false)
    check "signaled→Failed" (match run (Signaled("SIGKILL", "", "")) with Failed _ -> true | _ -> false)
    check "spawnFail→MissingExecutable"
        (match run (SpawnFailed(Wanxiangshu.Kernel.Domain.ExecutorExecutableMissing "bash")) with
         | MissingExecutable("bash", _) -> true
         | _ -> false)
    check "spawnFail(other)→Failed"
        (match run (SpawnFailed(Wanxiangshu.Kernel.Domain.SystemPanic "boom")) with
         | Failed _ -> true
         | _ -> false)
    equal "python exe uvx" "uvx" (Wanxiangshu.Shell.Executor.missingExecutableFor Python)

let capsFileShape () =
    let f : Wanxiangshu.Kernel.CapsFormat.CapsFile = { filePath = "/abs/HERE.md"; label = "HERE.md"; content = "x" }
    equal "capsFile filePath" "/abs/HERE.md" f.filePath
    equal "capsFile label" "HERE.md" f.label

let capsContextFormat () =
    let ctx = Wanxiangshu.Kernel.CapsFormat.buildCapitalsContext
                [ { filePath = "/abs/A & B.md"; label = "A & B.md"; content = "body text" } ]
    check "caps context is front matter" (ctx.StartsWith "---\ncaps:")
    check "caps label present" (ctx.Contains "A & B.md")
    check "caps content raw" (ctx.Contains "body text")

let capsFileSizeLimit () =
    equal "caps file size limit 4MB" (4 * 1_048_576) Wanxiangshu.Shell.WorkspaceFiles.maxFileSize

let stripHeadTailPipesOutsideQuotes () =
    let r = strip "cat f | head -n 5"
    equal "strip head -n 5 outside quotes" "cat f" r.script

let stripHeadTailPipesHeadTailChain () =
    let r = strip "cat a | head -n 20 | tail -5"
    equal "strip head then tail chain" "cat a" r.script

let readDirectoryListing () = promise {
    let! workspaceDir = mkdtempAsync "read-dir-"
    let nestedDir = unbox<string> (pathModule?join(workspaceDir, "nested"))
    let filePath = unbox<string> (pathModule?join(workspaceDir, "note.txt"))
    do! writeFileAsync filePath "hello"
    let fsAsync : obj = requireFn("fs")?promises
    do! unbox<JS.Promise<unit>> (fsAsync?mkdir(nestedDir))
    let! listing = Wanxiangshu.Shell.FileSys.read None workspaceDir None None
    check "directory listing contains file" (listing.Contains "note.txt")
    check "directory listing contains directory" (listing.Contains "nested")
    check "directory listing has total header" (listing.Contains "total 2")
    do! rmAsync workspaceDir
}

let ensureJavascriptProjectRepairsModuleType () = promise {
    let! projectDir = mkdtempAsync "executor-js-project-"
    let packageJsonPath = unbox<string> (pathModule?join(projectDir, "package.json"))
    do! writeFileAsync packageJsonPath "{\n  \"dependencies\": {\n    \"tsx\": \"*\"\n  }\n}\n"
    do! Wanxiangshu.Shell.ExecutorJavascript.ensureJavascriptProject projectDir []
    let fsAsync : obj = requireFn("fs")?promises
    let! packageJson = unbox<JS.Promise<string>> (fsAsync?readFile(packageJsonPath, "utf-8"))
    check "ensureJavascriptProject writes type module" (packageJson.Contains "\"type\": \"module\"")
    do! rmAsync projectDir
}

let private canRequireEsModuleLexer () : bool =
    try
        requireFn("es-module-lexer") |> ignore
        true
    with _ -> false

let rewriteJavascriptRelativeImports () = promise {
    if not (canRequireEsModuleLexer ()) then ()
    else
        let program = "import { x } from \"./foo.js\";\nconsole.log(x);\n"
        let! rewritten = Wanxiangshu.Shell.ExecutorJavascript.rewriteJavascriptModuleSpecifiers program "/abs/cwd"
        check "relative import rewritten to file URL" (rewritten.Contains "file:///")
        check "relative specifier consumed" (not (rewritten.Contains "\"./foo.js\""))
        check "non-relative body preserved" (rewritten.Contains "console.log(x)")
}

