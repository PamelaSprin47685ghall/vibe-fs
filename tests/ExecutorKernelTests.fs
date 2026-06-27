module Wanxiangshu.Tests.ExecutorKernelTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Executor

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
    equal "Short" 1000 (timeoutMs Short)
    equal "Long" 10000 (timeoutMs Long)
    equal "LastResort" 100_000 (timeoutMs LastResort)

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
    check "uses truncated body ref" (resp.Contains "/See Below, Truncated/")

let formatCompletedBodyRef () =
    let resp = formatToolResponse (Completed("x", 0)) None
    check "uses normal body ref" (resp.Contains "/See Below/")

let formatToolResponseFailedSignal () =
    let resp = formatToolResponse (Failed("partial", None, Some "SIGTERM")) None
    check "signal status" (resp.Contains "status: killed_signal")
    check "signal value" (resp.Contains "signal: SIGTERM")
    check "signal hint" (resp.Contains "Killed by signal SIGTERM")

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
    check "below threshold no summarize" (not (shouldSummarize byteLength (String.replicate 100 "x")))
    check "at threshold no summarize" (not (shouldSummarize byteLength (String.replicate 8192 "x")))
    check "above threshold summarize" (shouldSummarize byteLength (String.replicate 8193 "x"))

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
    equal "last-resort" LastResort (parseTimeout "last-resort")
    equal "LastResort" LastResort (parseTimeout "LastResort")
    equal "default short" Short (parseTimeout "short")
    equal "unknown falls back short" Short (parseTimeout "tiny")

let timeoutToStringRoundtrip () =
    equal "Short" "short" (timeoutToString Short)
    equal "Long" "long" (timeoutToString Long)
    equal "LastResort" "last-resort" (timeoutToString LastResort)

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
    let truncateToBytes (s: string) (max: int) = if s.Length <= max then s else s.Substring(0, max)
    let opts =
        { program = "echo x"; language = Shell; dependencies = []; timeoutType = Short
          mode = "ro"; cwd = None; whatToSummarize = "files" }
    let prompt = buildSummaryPrompt byteLength truncateToBytes opts (Completed("small output", 0))
    check "contains raw output" (prompt.Contains "small output")
    check "no truncation marker" (not (prompt.Contains "[Output truncated to 1MB for summarization]"))

let buildSummaryPromptLarge () =
    let byteLength (s: string) = s.Length
    let truncateToBytes (s: string) (max: int) = if s.Length <= max then s else s.Substring(0, max)
    let opts =
        { program = "echo x"; language = Shell; dependencies = []; timeoutType = Short
          mode = "ro"; cwd = None; whatToSummarize = "files" }
    let large = String.replicate 1_048_577 "x"
    let prompt = buildSummaryPrompt byteLength truncateToBytes opts (Completed(large, 0))
    check "large truncation marker present" (prompt.Contains "[Output truncated to 1MB for summarization]")

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
