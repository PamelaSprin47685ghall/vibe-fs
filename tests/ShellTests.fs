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

let recordValidator () =
    let parsePath (o: obj) = if string o = "" then Error "path required" else Ok o
    let result = VibeFs.Kernel.RecordValidator.validateRecord [ "path", parsePath ] (box {| path = "src/x.fs" |})
    check "valid ok" (Result.isOk result)
    let okObj = result |> Result.defaultValue (createObj [])
    equal "valid .path" "src/x.fs" (string (VibeFs.Kernel.Dyn.get okObj "path"))
    let bad = VibeFs.Kernel.RecordValidator.validateRecord [ "path", parsePath ] (box {| path = "" |})
    check "invalid errors" (Result.isError bad)
    let errObj = match bad with Error e -> e | _ -> createObj []
    equal "error .path message" "path required" (string (VibeFs.Kernel.Dyn.get errObj "path"))

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
    equal "caps file size limit 4MB" (4 * 1_048_576) VibeFs.Shell.CapsShell.maxFileSize

let ollamaFormat () =
    let results = [ { title = "A"; url = "u1"; content = "ca" }; { title = "B"; url = "u2"; content = "cb" } ]
    let formatted = VibeFs.Kernel.OllamaFormat.formatSearchResults results
    check "search numbering" (formatted.Contains "1. A" && formatted.Contains "2. B")
    equal "empty search" "No results found." (VibeFs.Kernel.OllamaFormat.formatSearchResults [])

let summarizerInputCap () =
    let opts : ExecuteOptions =
        { program = "echo x"; language = Shell; dependencies = []; timeoutType = Long; cwd = None }
    let small = String.replicate 100 "x"
    let smallPrompt = buildSummaryPrompt opts (Completed small)
    check "small output kept whole" (smallPrompt.Contains small)
    check "small output not truncated" (not (smallPrompt.Contains "[Output truncated to 1MB for summarization]"))
    let marker = "END_OF_OUTPUT_TAIL"
    let large = String.replicate (1_048_576 + 100 - marker.Length) "x" + marker
    let tail = marker
    let largePrompt = buildSummaryPrompt opts (Completed large)
    check "large output truncated message" (largePrompt.Contains "[Output truncated to 1MB for summarization]")
    check "large output tail absent" (not (largePrompt.Contains tail))
