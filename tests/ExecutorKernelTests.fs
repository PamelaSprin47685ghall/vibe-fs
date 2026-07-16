module Wanxiangshu.Tests.ExecutorKernelTests

open System.Text
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Runtime.ExecutorFormat
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.ExecutorToolsCodec
open Fable.Core.JsInterop

let private truncateUtf8 (s: string) (max: int) = truncateUtf8ByBytes s max

let private utf8ByteLength (s: string) =
    Encoding.UTF8.GetBytes s |> Array.length

// ── headTail ──────────────────────────────────────────────────────────

let headTailShort () =
    let r = headTail "hi" 5 5
    check "headTail short returns original" (r = "hi")

let headTailTruncate () =
    let r = headTail "abcdefghij" 2 3
    check "headTail truncates middle" (r = "ab...hij")

let headTailExactBoundary () =
    let r = headTail "abcde" 2 3
    check "headTail at boundary returns original" (r = "abcde")

let headTailLongerThanSum () =
    let r = headTail "abcdefghijklmnop" 3 3
    check "headTail longer than sum" (r = "abc...nop")

// ── timeoutMs ─────────────────────────────────────────────────────────

let timeoutMsValues () =
    equal "Short" 10_000 (timeoutMs Short)
    equal "Long" 100_000 (timeoutMs Long)

// ── outputFromResult ──────────────────────────────────────────────────

let outputFromResultAllVariants () =
    equal "Completed" "out" (outputFromResult (Completed("out", 0)))
    equal "Truncated" "part" (outputFromResult (Truncated("part", Short)))
    equal "Failed" "err" (outputFromResult (Failed("err", Some 1, None)))
    equal "MissingExecutable" "msg" (outputFromResult (MissingExecutable("bash", "msg")))

// ── formatToolResponse ────────────────────────────────────────────────

let formatCompletedNoSummary () =
    let resp = formatToolResponse (Completed("hello", 0)) None
    check "starts with ---" (resp.StartsWith "---")
    check "contains body" (resp.Contains "hello")
    check "contains status completed" (resp.Contains "status: completed")
    check "contains exit_code" (resp.Contains "exit_code: 0")

let formatCompletedWithSummary () =
    let resp = formatToolResponse (Completed("raw", 0)) (Some "SUMMARY")
    check "summary is body" (resp.Contains "SUMMARY")
    check "no raw output" (not (resp.Contains "raw"))

let formatTruncatedBodyRef () =
    let resp = formatToolResponse (Truncated("x", Short)) None
    check "uses truncated body ref" (resp.Contains "(Output Truncated)")

let formatCompletedBodyRef () =
    let resp = formatToolResponse (Completed("x", 0)) None
    check "no tool_output field" (not (resp.Contains "tool_output:"))

let formatToolResponseFailedSignal () =
    let resp = formatToolResponse (Failed("partial", None, Some "SIGTERM")) None
    check "signal status" (resp.Contains "status: SIGTERM")

let formatToolResponseFailedExitCode () =
    let resp = formatToolResponse (Failed("err", Some 2, None)) None
    check "exit error status" (resp.Contains "status: exit_error")
    check "exit code 2" (resp.Contains "exit_code: 2")

let formatToolResponseSpawnFailed () =
    let resp = formatToolResponse (Failed("spawn failed: ENOENT", None, None)) None
    check "spawn failed status" (resp.Contains "status: spawn_failed")

let formatToolResponseMissingExec () =
    let resp = formatToolResponse (MissingExecutable("bash", "not found")) None
    check "missing exec status" (resp.Contains "status: missing_executable")

// ── shouldAppendReadOnlyWarning ───────────────────────────────────────

let readOnlyShellCommands () =
    check "head warns" (shouldAppendReadOnlyWarning "head -n 1 file" Shell)
    check "tail warns" (shouldAppendReadOnlyWarning "tail -n 1 file" Shell)
    check "cat warns" (shouldAppendReadOnlyWarning "cat file" Shell)
    check "grep warns" (shouldAppendReadOnlyWarning "grep foo file" Shell)
    check "rg warns" (shouldAppendReadOnlyWarning "rg pattern" Shell)
    check "find warns" (shouldAppendReadOnlyWarning "find . -name x" Shell)
    check "sed warns" (shouldAppendReadOnlyWarning "sed 's/a/b/' file" Shell)
    check "grep in pipe warns" (shouldAppendReadOnlyWarning "ls | grep foo" Shell)
    check "prefixed grep warns" (shouldAppendReadOnlyWarning "/usr/bin/grep foo" Shell)

let noWarningNonReadCommand () =
    check "echo no warn" (not (shouldAppendReadOnlyWarning "echo hi" Shell))
    check "printf no warn" (not (shouldAppendReadOnlyWarning "printf hi" Shell))

let noWarningNonShell () =
    check "python no warn" (not (shouldAppendReadOnlyWarning "head -n 1" Python))
    check "javascript no warn" (not (shouldAppendReadOnlyWarning "head -n 1" Javascript))

// ── shouldSummarize ───────────────────────────────────────────────────

let shouldSummarizeThreshold () =
    let byteLength (s: string) = s.Length
    let maxBytes = 100
    check "below threshold no summarize" (not (shouldSummarize byteLength maxBytes (String.replicate 99 "x")))
    check "at threshold no summarize" (not (shouldSummarize byteLength maxBytes (String.replicate 100 "x")))
    check "above threshold summarize" (shouldSummarize byteLength maxBytes (String.replicate 101 "x"))

    let sameString = String.replicate 5000 "x"
    check "shouldSummarize fires at non-default threshold 4096" (shouldSummarize byteLength 4096 sameString)

    check
        "shouldSummarize does not fire at non-default threshold 8192"
        (not (shouldSummarize byteLength 8192 sameString))

let decodeExecutorArgsMissingMaxBytes () =
    let args =
        createObj
            [ "command", box "echo hi"
              "mode", box "ro"
              "what_to_summarize", box "files"
              "timeout_type", box "short" ]

    let result = decodeExecutorArgs args

    match result with
    | Error(InvalidIntent("executor", "max_bytes", "required")) -> check "missing max_bytes recognized" true
    | _ -> failwith "expected missing max_bytes error"

let decodeExecutorArgsValidMaxBytes () =
    let args =
        createObj
            [ "command", box "echo hi"
              "mode", box "ro"
              "what_to_summarize", box "files"
              "max_bytes", box 4096
              "timeout_type", box "short" ]

    let result = decodeExecutorArgs args

    match result with
    | Ok(args) when args.MaxBytes = 4096 -> check "max_bytes 4096 decoded" true
    | _ -> failwith "expected MaxBytes = 4096"

// ── prependSafetyWarning ──────────────────────────────────────────────

let prependWarningReadCommand () =
    let out = prependSafetyWarning "OUT" "grep foo" Shell
    check "warning prepended for grep" (out.Contains "hint")
    check "output body preserved" (out.Contains "OUT")

let prependWarningNonRead () =
    let out = prependSafetyWarning "OUT" "echo hi" Shell
    check "no warning for echo" (out = "OUT")

let prependWarningNonShell () =
    let out = prependSafetyWarning "OUT" "head -n 1" Python
    check "no warning in python" (out = "OUT")

// ── parseLanguage / languageToString ──────────────────────────────────

let parseLanguageRoundtrip () =
    equal "python lower" Python (parseLanguage "python")
    equal "javascript lower" Javascript (parseLanguage "javascript")
    equal "shell default" Shell (parseLanguage "shell")
    equal "Python capital" Python (parseLanguage "Python")
    equal "unknown falls back to Shell" Shell (parseLanguage "unknown")

let languageToStringRoundtrip () =
    equal "Python" "python" (languageToString Python)
    equal "Javascript" "javascript" (languageToString Javascript)
    equal "Shell" "shell" (languageToString Shell)

// ── parseTimeout / timeoutToString ────────────────────────────────────

let parseTimeoutRoundtrip () =
    equal "long" Long (parseTimeout "long")
    equal "default short" Short (parseTimeout "short")
    equal "unknown falls back short" Short (parseTimeout "tiny")

let timeoutToStringRoundtrip () =
    equal "Short" "short" (timeoutToString Short)
    equal "Long" "long" (timeoutToString Long)

// ── prepareShellProgram ───────────────────────────────────────────────

let prepareShellStripsPipes () =
    let r = prepareShellProgram "printf hi | head -n 1"
    check "pipe stripped" (r = "printf hi")

let prepareShellKeepsNormal () =
    let r = prepareShellProgram "echo hello"
    check "no pipe kept" (r = "echo hello")

// ── buildSummaryPrompt ────────────────────────────────────────────────

let buildSummaryPromptSmall () =
    let byteLength (s: string) = s.Length

    let truncateToBytes (s: string) (max: int) =
        if s.Length <= max then s else s.Substring(0, max)

    let opts =
        { command = "echo x"
          language = Shell
          dependencies = []
          timeoutType = Short
          mode = "ro"
          cwd = None
          whatToSummarize = "files"
          maxBytes = 8192 }

    let prompt =
        buildSummaryPrompt byteLength truncateToBytes opts (Completed("small output", 0))

    check "contains raw output" (prompt.Contains "small output")
    check "no truncation marker" (not (prompt.Contains "[Output truncated to 200000 bytes for summarization]"))

let buildSummaryPromptLarge () =
    let byteLength (s: string) = utf8ByteLength s

    let opts =
        { command = "echo x"
          language = Shell
          dependencies = []
          timeoutType = Short
          mode = "ro"
          cwd = None
          whatToSummarize = "files"
          maxBytes = 8192 }

    let large = String.replicate 200_001 "x"
    let prompt = buildSummaryPrompt byteLength truncateUtf8 opts (Completed(large, 0))
    check "large truncation marker present" (prompt.Contains "[Output truncated to 200000 bytes for summarization]")

let buildSummaryPromptUtf8Boundary () =
    let byteLength (s: string) = utf8ByteLength s

    let opts =
        { command = "echo x"
          language = Shell
          dependencies = []
          timeoutType = Short
          mode = "ro"
          cwd = None
          whatToSummarize = "files"
          maxBytes = 8192 }

    let raw = String.replicate 66_667 "你"
    let prompt = buildSummaryPrompt byteLength truncateUtf8 opts (Completed(raw, 0))
    check "utf8 truncation marker present" (prompt.Contains "[Output truncated to 200000 bytes for summarization]")
    check "utf8 truncation preserves whole chars" (not (prompt.Contains "�"))
    check "utf8 truncation excludes partial tail" (not (prompt.EndsWith "你"))

// ── run ───────────────────────────────────────────────────────────────

let run () : unit =
    headTailShort ()
    headTailTruncate ()
    headTailExactBoundary ()
    headTailLongerThanSum ()
    timeoutMsValues ()
    outputFromResultAllVariants ()
    formatCompletedNoSummary ()
    formatCompletedWithSummary ()
    formatTruncatedBodyRef ()
    formatCompletedBodyRef ()
    formatToolResponseFailedSignal ()
    formatToolResponseFailedExitCode ()
    formatToolResponseSpawnFailed ()
    formatToolResponseMissingExec ()
    readOnlyShellCommands ()
    noWarningNonReadCommand ()
    noWarningNonShell ()
    shouldSummarizeThreshold ()
    decodeExecutorArgsMissingMaxBytes ()
    decodeExecutorArgsValidMaxBytes ()
    prependWarningReadCommand ()
    prependWarningNonRead ()
    prependWarningNonShell ()
    parseLanguageRoundtrip ()
    languageToStringRoundtrip ()
    parseTimeoutRoundtrip ()
    timeoutToStringRoundtrip ()
    prepareShellStripsPipes ()
    prepareShellKeepsNormal ()
    buildSummaryPromptSmall ()
    buildSummaryPromptLarge ()
    buildSummaryPromptUtf8Boundary ()
