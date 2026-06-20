module VibeFs.Tests.ShellTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Kernel.Executor
open VibeFs.Kernel.Prompts

[<Import("createRequire", "node:module")>]
let private createRequire' : string -> (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta : obj = jsNative

let private requireFn : string -> obj = createRequire'(string importMeta?url)
let private pathModule : obj = requireFn("path")

let ollamaFetchInit () =
    let init = VibeFs.Shell.OllamaClient.postInit "KEY123" "{\"a\":1}" None
    equal "init method POST" "POST" (VibeFs.Kernel.Dyn.str init "method")
    let headers = VibeFs.Kernel.Dyn.get init "headers"
    equal "init Content-Type" "application/json" (VibeFs.Kernel.Dyn.str headers "Content-Type")
    let auth = VibeFs.Kernel.Dyn.str headers "Authorization"
    check "init Authorization key" (auth.Contains "KEY123")
    equal "init body json" "{\"a\":1}" (VibeFs.Kernel.Dyn.str init "body")
    check "init no signal when None" (VibeFs.Kernel.Dyn.isNullish (VibeFs.Kernel.Dyn.get init "signal"))
    let withSignal = VibeFs.Shell.OllamaClient.postInit "K" "b" (Some (box "ABORT"))
    check "init signal when Some" (not (VibeFs.Kernel.Dyn.isNullish (VibeFs.Kernel.Dyn.get withSignal "signal")))

let ollamaResponseMethodCall () =
    let response =
        createObj
            [ "text", box (fun () -> "body")
              "json", box (fun () -> createObj [ "ok", box "yes" ]) ]
    equal "response.text() invoked" "body" (unbox<string> (VibeFs.Shell.OllamaClient.responseMethod0 response "text"))
    let json = VibeFs.Shell.OllamaClient.responseMethod0 response "json"
    equal "response.json() invoked" "yes" (VibeFs.Kernel.Dyn.str json "ok")

let ollamaApiKeyValidation () =
    equal "requireOllamaApiKey trims" (Ok "KEY123") (VibeFs.Shell.OllamaClient.requireOllamaApiKey "  KEY123  ")
    equal "requireOllamaApiKey rejects missing" (Error "Missing OLLAMA_API_KEY environment variable.") (VibeFs.Shell.OllamaClient.requireOllamaApiKey "")
    equal "requireOllamaApiKey rejects empty" (Error "Missing OLLAMA_API_KEY environment variable.") (VibeFs.Shell.OllamaClient.requireOllamaApiKey "   ")

let executorMapping () =
    let opts : ExecuteOptions =
        { program = "echo x"; language = Shell; dependencies = []; timeoutType = Long; cwd = None }
    let run o = VibeFs.Shell.Executor.mapOutcome opts 10000 "out" o
    check "exit0→Completed" (match run { stdout=""; stderr=""; code=Some 0; timedOut=false } with Completed _ -> true | _ -> false)
    check "nonzero→Failed" (match run { stdout=""; stderr=""; code=Some 2; timedOut=false } with Failed _ -> true | _ -> false)
    check "timeout→Truncated" (match run { stdout=""; stderr=""; code=None; timedOut=true } with Truncated _ -> true | _ -> false)
    equal "python exe uvx" "uvx" (VibeFs.Shell.Executor.missingExecutableFor Python)

let capsFileShape () =
    let f : VibeFs.Kernel.CapsFormat.CapsFile = { filePath = "/abs/HERE.md"; label = "HERE.md"; content = "x" }
    equal "capsFile filePath" "/abs/HERE.md" f.filePath
    equal "capsFile label" "HERE.md" f.label

let capsContextFormat () =
    let ctx = VibeFs.Kernel.CapsFormat.buildCapitalsContext
                [ { filePath = "/abs/A & B.md"; label = "A & B.md"; content = "body text" } ]
    check "caps context is front matter" (ctx.StartsWith "---\ncaps:")
    check "caps label present" (ctx.Contains "A & B.md")
    check "caps content raw" (ctx.Contains "body text")

let capsFileSizeLimit () =
    equal "caps file size limit 4MB" (4 * 1_048_576) VibeFs.Shell.WorkspaceFiles.maxFileSize

let readDirectoryListing () = promise {
    let! workspaceDir = mkdtempAsync "read-dir-"
    let nestedDir = unbox<string> (pathModule?join(workspaceDir, "nested"))
    let filePath = unbox<string> (pathModule?join(workspaceDir, "note.txt"))
    do! writeFileAsync filePath "hello"
    let fsAsync : obj = requireFn("fs")?promises
    do! unbox<JS.Promise<unit>> (fsAsync?mkdir(nestedDir))
    let! listing = VibeFs.Shell.FileSys.read None workspaceDir None None
    check "directory listing contains file" (listing.Contains "note.txt")
    check "directory listing contains directory" (listing.Contains "nested")
    check "directory listing has total header" (listing.Contains "total 2")
    do! rmAsync workspaceDir
}

let ensureJavascriptProjectRepairsModuleType () = promise {
    let! projectDir = mkdtempAsync "executor-js-project-"
    let packageJsonPath = unbox<string> (pathModule?join(projectDir, "package.json"))
    do! writeFileAsync packageJsonPath "{\n  \"dependencies\": {\n    \"tsx\": \"*\"\n  }\n}\n"
    do! VibeFs.Shell.ExecutorJavascript.ensureJavascriptProject projectDir []
    let fsAsync : obj = requireFn("fs")?promises
    let! packageJson = unbox<JS.Promise<string>> (fsAsync?readFile(packageJsonPath, "utf-8"))
    check "ensureJavascriptProject writes type module" (packageJson.Contains "\"type\": \"module\"")
    do! rmAsync projectDir
}

/// Exercises parseImports: es-module-lexer's `init()` returns a native JS Promise
/// that MUST be awaited before `parse()`. The Promise.lift regression wrapped it
/// without awaiting, so parse ran before the WASM lexer was ready and the call
/// rejected — leaving relative specifiers un-rewritten. Skipped gracefully when
/// es-module-lexer (an optionalDependency) is absent from this environment.
let private canRequireEsModuleLexer () : bool =
    try
        requireFn("es-module-lexer") |> ignore
        true
    with _ -> false

let rewriteJavascriptRelativeImports () = promise {
    if not (canRequireEsModuleLexer ()) then ()
    else
        let program = "import { x } from \"./foo.js\";\nconsole.log(x);\n"
        let! rewritten = VibeFs.Shell.ExecutorJavascript.rewriteJavascriptModuleSpecifiers program "/abs/cwd"
        check "relative import rewritten to file URL" (rewritten.Contains "file:///")
        check "relative specifier consumed" (not (rewritten.Contains "\"./foo.js\""))
        check "non-relative body preserved" (rewritten.Contains "console.log(x)")
}

let wikiPortRangeSpec () = promise {
    let portA = VibeFs.Shell.WikiPortLock.lockPortForPath "/tmp/wiki-a"
    let portB = VibeFs.Shell.WikiPortLock.lockPortForPath "/tmp/wiki-a"
    check "wiki lock deterministic" (portA = portB)
    check "wiki lock in high range" (portA >= 49152 && portA < 65536)
}

let wikiPortSerialSpec () = promise {
    let seen = System.Collections.Generic.List<string>()
    let first =
        VibeFs.Shell.WikiPortLock.withWikiPortLock "/tmp/wiki-lock-test" (fun () -> promise {
            seen.Add "first-start"
            do! Promise.sleep 50
            seen.Add "first-end"
            return "one"
        })
    let second =
        VibeFs.Shell.WikiPortLock.withWikiPortLock "/tmp/wiki-lock-test" (fun () -> promise {
            seen.Add "second-start"
            seen.Add "second-end"
            return "two"
        })
    let! _ = first
    let! _ = second
    check "wiki lock serializes same workspace" (seen |> Seq.toArray = [| "first-start"; "first-end"; "second-start"; "second-end" |])
}

let ollamaFormat () =
    let results = [ { title = "A"; url = "u1"; content = "ca" }; { title = "B"; url = "u2"; content = "cb" } ]
    let formatted = VibeFs.Kernel.Prompts.formatSearchResults results
    check "search results front matter" (formatted.StartsWith "---\nresults:")
    check "search embeds title A" (formatted.Contains "title: \"A\"")
    check "search embeds title B" (formatted.Contains "title: \"B\"")
    equal "empty search" "No results found." (VibeFs.Kernel.Prompts.formatSearchResults [])

let summarizerInputCap () =
    let bl (s: string) : int = s.Length
    let trunc (s: string) (maxBytes: int) : string = if s.Length <= maxBytes then s else s.[..maxBytes - 1]
    let opts : ExecuteOptions =
        { program = "echo x"; language = Shell; dependencies = []; timeoutType = Long; cwd = None }
    let small = String.replicate 100 "x"
    let smallPrompt = buildSummaryPrompt bl trunc opts (Completed small)
    check "small output kept whole" (smallPrompt.Contains small)
    check "small output not truncated" (not (smallPrompt.Contains "[Output truncated to 1MB for summarization]"))
    let marker = "END_OF_OUTPUT_TAIL"
    let large = String.replicate (1_048_576 + 100 - marker.Length) "x" + marker
    let tail = marker
    let largePrompt = buildSummaryPrompt bl trunc opts (Completed large)
    check "large output truncated message" (largePrompt.Contains "[Output truncated to 1MB for summarization]")
    check "large output tail absent" (not (largePrompt.Contains tail))

let safetyWarning () =
    let warn program = prependSafetyWarning "OUT" program Shell
    let warnForExecution program =
        prependSafetyWarningForExecution "OUT" { program = program; language = Shell; dependencies = []; timeoutType = Short; cwd = None }
    check "leading grep warns" ((warn "grep foo").Contains readOnlyWarning)
    check "grep after && warns" ((warn "cd src && grep foo").Contains readOnlyWarning)
    check "grep in pipe warns" ((warn "ls a | grep b").Contains readOnlyWarning)
    check "stripped head pipe passes" (not ((warn "printf hi | head -n 1").Contains readOnlyWarning))
    check "execution warning uses prepared program" (not ((warnForExecution "printf hi | head -n 1").Contains readOnlyWarning))
    check "real head command warns" ((warn "head -n 1 file.txt").Contains readOnlyWarning)
    check "ls after semicolon warns" ((warn "echo ok; ls -la").Contains readOnlyWarning)
    check "prefixed path warns" ((warn "/usr/bin/grep foo").Contains readOnlyWarning)
    check "plain echo passes" (not ((warn "echo hi").Contains readOnlyWarning))
    check "substring inside word ignored" (not ((warn "echo concatenate").Contains readOnlyWarning))
    check "non-shell language ignored" (not ((prependSafetyWarning "OUT" "grep foo" Python).Contains readOnlyWarning))
