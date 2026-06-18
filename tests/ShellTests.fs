module VibeFs.Tests.ShellTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.ExecutorKernel
open VibeFs.Kernel.OllamaFormat

let ollamaFetchInit () =
    let init = VibeFs.Shell.OllamaClient.postInitNoSignal "KEY123" "{\"a\":1}"
    equal "init method POST" "POST" (VibeFs.Kernel.Dyn.str init "method")
    let headers = VibeFs.Kernel.Dyn.get init "headers"
    equal "init Content-Type" "application/json" (VibeFs.Kernel.Dyn.str headers "Content-Type")
    let auth = VibeFs.Kernel.Dyn.str headers "Authorization"
    check "init Authorization key" (auth.Contains "KEY123")
    equal "init body json" "{\"a\":1}" (VibeFs.Kernel.Dyn.str init "body")
    check "init no signal when None" (VibeFs.Kernel.Dyn.isNullish (VibeFs.Kernel.Dyn.get init "signal"))
    let withSignal = VibeFs.Shell.OllamaClient.postInitWithSignal "K" "b" (box "ABORT")
    check "init signal when Some" (not (VibeFs.Kernel.Dyn.isNullish (VibeFs.Kernel.Dyn.get withSignal "signal")))

let ollamaResponseMethodCall () =
    let response =
        createObj
            [ "text", box (fun () -> "body")
              "json", box (fun () -> createObj [ "ok", box "yes" ]) ]
    equal "response.text() invoked" "body" (unbox<string> (VibeFs.Shell.OllamaClient.responseMethod0 response "text"))
    let json = VibeFs.Shell.OllamaClient.responseMethod0 response "json"
    equal "response.json() invoked" "yes" (VibeFs.Kernel.Dyn.str json "ok")

let executorMapping () =
    let opts : ExecuteOptions =
        { program = "echo x"; language = Shell; dependencies = []; timeoutType = Long; cwd = None }
    let run o = VibeFs.Shell.ExecutorShell.mapOutcome opts 10000 "out" o
    check "exit0→Completed" (match run { stdout=""; stderr=""; code=Some 0; timedOut=false } with Completed _ -> true | _ -> false)
    check "nonzero→Failed" (match run { stdout=""; stderr=""; code=Some 2; timedOut=false } with Failed _ -> true | _ -> false)
    check "timeout→Truncated" (match run { stdout=""; stderr=""; code=None; timedOut=true } with Truncated _ -> true | _ -> false)
    equal "python exe uvx" "uvx" (VibeFs.Shell.ExecutorShell.missingExecutableFor Python)

let capsFileShape () =
    let f : VibeFs.Kernel.CapsFormat.CapsFile = { filePath = "/abs/HERE.md"; label = "HERE.md"; content = "x" }
    equal "capsFile filePath" "/abs/HERE.md" f.filePath
    equal "capsFile label" "HERE.md" f.label

let capsContextFormat () =
    let ctx = VibeFs.Kernel.CapsFormat.buildCapitalsContext
                [ { filePath = "/abs/A & B.md"; label = "A & B.md"; content = "body text" } ]
    check "caps file= label escaped" (ctx.Contains "file=\"A &amp; B.md\"")
    check "caps content raw" (ctx.Contains "body text")

let capsFileSizeLimit () =
    equal "caps file size limit 4MB" (4 * 1_048_576) VibeFs.Shell.Caps.maxFileSize

let ollamaFormat () =
    let results = [ { title = "A"; url = "u1"; content = "ca" }; { title = "B"; url = "u2"; content = "cb" } ]
    let formatted = VibeFs.Kernel.OllamaFormat.formatSearchResults results
    check "search numbering" (formatted.Contains "1. A" && formatted.Contains "2. B")
    equal "empty search" "No results found." (VibeFs.Kernel.OllamaFormat.formatSearchResults [])

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
    check "leading grep warns" ((warn "grep foo").Contains readOnlyWarning)
    check "grep after && warns" ((warn "cd src && grep foo").Contains readOnlyWarning)
    check "grep in pipe warns" ((warn "ls a | grep b").Contains readOnlyWarning)
    check "ls after semicolon warns" ((warn "echo ok; ls -la").Contains readOnlyWarning)
    check "prefixed path warns" ((warn "/usr/bin/grep foo").Contains readOnlyWarning)
    check "plain echo passes" (not ((warn "echo hi").Contains readOnlyWarning))
    check "substring inside word ignored" (not ((warn "echo concatenate").Contains readOnlyWarning))
    check "non-shell language ignored" (not ((prependSafetyWarning "OUT" "grep foo" Python).Contains readOnlyWarning))
