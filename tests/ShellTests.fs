module VibeFs.Tests.ShellTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Kernel.Executor
open VibeFs.Kernel.ToolOutputInfo
open VibeFs.Kernel.SubagentPrompts
open VibeFs.Kernel.SearchPrompts
open VibeFs.Shell
open VibeFs.Shell.Dyn

[<Import("createRequire", "node:module")>]
let private createRequire' : string -> (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta : obj = jsNative

let private requireFn : string -> obj = createRequire'(string importMeta?url)
let private pathModule : obj = requireFn("path")

let ollamaFetchInit () =
    let init = VibeFs.Shell.OllamaClient.postInit "KEY123" "{\"a\":1}" None
    equal "init method POST" "POST" (VibeFs.Shell.Dyn.str init "method")
    let headers = VibeFs.Shell.Dyn.get init "headers"
    equal "init Content-Type" "application/json" (VibeFs.Shell.Dyn.str headers "Content-Type")
    let auth = VibeFs.Shell.Dyn.str headers "Authorization"
    check "init Authorization key" (auth.Contains "KEY123")
    equal "init body json" "{\"a\":1}" (VibeFs.Shell.Dyn.str init "body")
    check "init no signal when None" (VibeFs.Shell.Dyn.isNullish (VibeFs.Shell.Dyn.get init "signal"))
    let withSignal = VibeFs.Shell.OllamaClient.postInit "K" "b" (Some (box "ABORT"))
    check "init signal when Some" (not (VibeFs.Shell.Dyn.isNullish (VibeFs.Shell.Dyn.get withSignal "signal")))

let ollamaResponseMethodCall () =
    let response =
        createObj
            [ "text", box (fun () -> "body")
              "json", box (fun () -> createObj [ "ok", box "yes" ]) ]
    equal "response.text() invoked" "body" (unbox<string> (VibeFs.Shell.OllamaClient.responseMethod0 response "text"))
    let json = VibeFs.Shell.OllamaClient.responseMethod0 response "json"
    equal "response.json() invoked" "yes" (VibeFs.Shell.Dyn.str json "ok")

let ollamaApiKeyValidation () =
    equal "requireOllamaApiKey trims" (Ok "KEY123") (VibeFs.Shell.OllamaClient.requireOllamaApiKey "  KEY123  ")
    equal "requireOllamaApiKey rejects missing" (Error "Missing OLLAMA_API_KEY environment variable.") (VibeFs.Shell.OllamaClient.requireOllamaApiKey "")
    equal "requireOllamaApiKey rejects empty" (Error "Missing OLLAMA_API_KEY environment variable.") (VibeFs.Shell.OllamaClient.requireOllamaApiKey "   ")

let executorMapping () =
    let opts : ExecuteOptions =
        { program = "echo x"; language = Shell; dependencies = []; timeoutType = Long; mode = "ro"; cwd = None }
    let run o = VibeFs.Shell.Executor.mapOutcome opts 10000 "out" o
    check "exit0→Completed" (match run (VibeFs.Shell.Executor.Exited(0, "", "")) with Completed _ -> true | _ -> false)
    check "nonzero→Failed" (match run (VibeFs.Shell.Executor.Exited(2, "", "")) with Failed _ -> true | _ -> false)
    check "timeout→Truncated" (match run (VibeFs.Shell.Executor.TimedOut("", "")) with Truncated _ -> true | _ -> false)
    check "signaled→Failed" (match run (VibeFs.Shell.Executor.Signaled("SIGKILL", "", "")) with Failed _ -> true | _ -> false)
    check "spawnFail→MissingExecutable"
        (match run (VibeFs.Shell.Executor.SpawnFailed(VibeFs.Kernel.Domain.ExecutorExecutableMissing "bash")) with
         | MissingExecutable("bash", _) -> true
         | _ -> false)
    check "spawnFail(other)→Failed"
        (match run (VibeFs.Shell.Executor.SpawnFailed(VibeFs.Kernel.Domain.SystemPanic "boom")) with
         | Failed _ -> true
         | _ -> false)
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

let knowledgeGraphPortRangeSpec () = promise {
    let portA = VibeFs.Shell.KnowledgeGraphPortLock.lockPortForPath "/tmp/kg-a"
    let portB = VibeFs.Shell.KnowledgeGraphPortLock.lockPortForPath "/tmp/kg-a"
    check "knowledge graph lock deterministic" (portA = portB)
    check "knowledge graph lock in high range" (portA >= 49152 && portA < 65536)
}

let knowledgeGraphPortSerialSpec () = promise {
    let seen = System.Collections.Generic.List<string>()
    let firstAcquiredResolve = ref (fun () -> ())
    let firstAcquired : JS.Promise<unit> = Promise.create (fun resolve _ -> firstAcquiredResolve.Value <- resolve)
    let releaseFirst = ref (fun () -> ())
    let firstGate : JS.Promise<unit> = Promise.create (fun resolve _ -> releaseFirst.Value <- resolve)
    let first =
        VibeFs.Shell.KnowledgeGraphPortLock.withKnowledgeGraphPortLock 30000L 0 "/tmp/kg-lock-test" (fun () -> promise {
            seen.Add "first-start"
            firstAcquiredResolve.Value ()
            do! firstGate
            seen.Add "first-end"
            return "one"
        })
    do! firstAcquired
    let second =
        VibeFs.Shell.KnowledgeGraphPortLock.withKnowledgeGraphPortLock 30000L 0 "/tmp/kg-lock-test" (fun () -> promise {
            seen.Add "second-start"
            seen.Add "second-end"
            return "two"
        })
    releaseFirst.Value ()
    let! _ = first
    let! _ = second
    check "knowledge graph lock serializes same workspace" (seen |> Seq.toArray = [| "first-start"; "first-end"; "second-start"; "second-end" |])
}

let ollamaFormat () =
    let results = [ { title = "A"; url = "u1"; content = "ca" }; { title = "B"; url = "u2"; content = "cb" } ]
    let formatted = VibeFs.Kernel.SearchPrompts.formatSearchResults results
    check "search results front matter" (formatted.StartsWith "---\nresults:")
    check "search embeds title A" (formatted.Contains "title: A")
    check "search embeds title B" (formatted.Contains "title: B")
    equal "empty search" "No results found." (VibeFs.Kernel.SearchPrompts.formatSearchResults [])

let safetyWarning () =
    let warn program = prependSafetyWarning "OUT" program Shell
    let warnForExecution program =
        prependSafetyWarningForExecution "OUT" { program = program; language = Shell; dependencies = []; timeoutType = Short; mode = "ro"; cwd = None }
    check "leading grep warns" (hasExactHint (warn "grep foo") hintExecutorMisuse)
    check "grep after && warns" (hasExactHint (warn "cd src && grep foo") hintExecutorMisuse)
    check "grep in pipe warns" (hasExactHint (warn "ls a | grep b") hintExecutorMisuse)
    check "stripped head pipe passes" (not (hasExactHint (warn "printf hi | head -n 1") hintExecutorMisuse))
    check "execution warning uses prepared program" (not (hasExactHint (warnForExecution "printf hi | head -n 1") hintExecutorMisuse))
    check "real head command warns" (hasExactHint (warn "head -n 1 file.txt") hintExecutorMisuse)
    check "ls after semicolon warns" (hasExactHint (warn "echo ok; ls -la") hintExecutorMisuse)
    check "prefixed path warns" (hasExactHint (warn "/usr/bin/grep foo") hintExecutorMisuse)
    check "plain echo passes" (not (hasExactHint (warn "echo hi") hintExecutorMisuse))
    check "substring inside word ignored" (not (hasExactHint (warn "echo concatenate") hintExecutorMisuse))
    check "non-shell language ignored" (not (hasExactHint (prependSafetyWarning "OUT" "grep foo" Python) hintExecutorMisuse))

/// Executor tool output must prepend a structured return YAML block
/// carrying exit_code + status. Tests expect extended ExecuteResult with exit
/// metadata: Completed(output, exitCode), Failed(output, exitCodeOpt, signalOpt).
let executorToolResponseFormatting () =
    let completedResult = Completed("all good", 0)
    let failedResult = Failed("boom", Some 2, None)
    let truncatedResult = Truncated("partial", Long)
    let missingResult = MissingExecutable("bash", "Error: not found")

    equal "outputFromResult completed" "all good" (outputFromResult completedResult)
    equal "outputFromResult failed" "boom" (outputFromResult failedResult)
    equal "outputFromResult truncated" "partial" (outputFromResult truncatedResult)
    equal "outputFromResult missing" "Error: not found" (outputFromResult missingResult)

    let resp = formatToolResponse completedResult None
    check "response prepends return block" (resp.StartsWith "---")
    check "response includes output body" (resp.Contains "all good")
    check "response includes exit_code" (resp.Contains "exit_code: 0")
    check "response includes status completed" (resp.Contains "status: completed")

    let failedResp = formatToolResponse failedResult None
    check "failed response includes exit_code 2" (failedResp.Contains "exit_code: 2")
    check "failed response includes status exit_error" (failedResp.Contains "status: exit_error")

    let truncatedResp = formatToolResponse truncatedResult None
    check "truncated response includes status killed_timeout" (truncatedResp.Contains "status: killed_timeout")
    check "truncated response uses truncated body ref" (truncatedResp.Contains seeBelowTruncated)
    check "truncated response includes timeout_ms" (truncatedResp.Contains "timeout_ms:")
    check "truncated response omits timeout_type" (not (truncatedResp.Contains "timeout_type:"))
    check "truncated response includes killed hint in info" (hintTextContains truncatedResp "Killed after")
    check "truncated body excludes legacy executor suffix" (not (truncatedResp.Contains "[executor]"))

    let signaledResult = Failed("partial out", None, Some "SIGTERM")
    let signaledResp = formatToolResponse signaledResult None
    check "signaled response includes status killed_signal" (signaledResp.Contains "status: killed_signal")
    check "signaled response includes signal" (signaledResp.Contains "signal: SIGTERM")
    check "signaled response includes killed hint" (hintTextContains signaledResp "Killed by signal")
    check "signaled body has no legacy suffix" (not (signaledResp.Contains "[executor]"))

    let missingResp = formatToolResponse missingResult None
    check "missing response includes status missing_executable" (missingResp.Contains "status: missing_executable")

    let summary = "SUMMARY: task succeeded"
    let summaryResp = formatToolResponse completedResult (Some summary)
    check "summary response prepends return block" (summaryResp.StartsWith "---")
    check "summary response uses summary as body" (summaryResp.Contains summary)
    check "summary response has exit_code 0" (summaryResp.Contains "exit_code: 0")

/// When output is summarized, the summarizer prompt must NOT instruct the model
/// to preserve exit status — that metadata now travels in the structured return
/// block prepended by formatToolResponse.
let summarizerPromptOmitsReturnValue () =
    let prompt = executorSummarizerPrompt "raw output" "shell" "echo 1" [] "short" "ro"
    check "summarizer prompt omits exit status" (not (prompt.Contains "exit status"))
    check "summarizer prompt omits non-zero" (not (prompt.ToLowerInvariant().Contains "non-zero"))
    check "summarizer empty deps yaml" (prompt.Contains "dependencies: []")
    let multiline =
        executorSummarizerPrompt "line1\nline2" "shell" "echo hi\necho bye" [ "dep1" ] "long" "ro"
    check "summarizer multiline program uses block field" (multiline.Contains "program: |")
    check "summarizer multiline raw_output uses block field" (multiline.Contains "raw_output: |")
