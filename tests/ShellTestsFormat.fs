module Wanxiangshu.Tests.ShellTestsFormat

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Kernel.SearchPrompts
open Wanxiangshu.Kernel.SubagentPrompts
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Shell

let webApiSearchFormat () =
    let results =
        [ { title = "A"
            url = "u1"
            content = "ca" }
          { title = "B"
            url = "u2"
            content = "cb" } ]

    let formatted = formatSearchResults results
    check "search results front matter" (formatted.StartsWith "---\nresults:")
    check "search embeds title A" (formatted.Contains "title: A")
    check "search embeds title B" (formatted.Contains "title: B")
    equal "empty search" "No results found." (formatSearchResults [])

let ollamaFormat = webApiSearchFormat

let summarizerInputCap () =
    let bl (s: string) : int = s.Length

    let trunc (s: string) (maxBytes: int) : string =
        if s.Length <= maxBytes then s else s.[.. maxBytes - 1]

    let opts: ExecuteOptions =
        { program = "echo x"
          language = Shell
          dependencies = []
          timeoutType = Long
          mode = "ro"
          cwd = None
          whatToSummarize = "" }

    let small = String.replicate 100 "x"
    let smallPrompt = buildSummaryPrompt bl trunc opts (Completed(small, 0))
    check "small output kept whole" (smallPrompt.Contains small)

    check
        "small output not truncated"
        (not (smallPrompt.Contains "[Output truncated to 200000 bytes for summarization]"))

    let marker = "END_OF_OUTPUT_TAIL"
    let large = String.replicate (200_000 + 100 - marker.Length) "x" + marker
    let tail = marker
    let largePrompt = buildSummaryPrompt bl trunc opts (Completed(large, 0))
    check "large output truncated message" (largePrompt.Contains "[Output truncated to 200000 bytes for summarization]")
    check "large output tail absent" (not (largePrompt.Contains tail))

let safetyWarning () =
    let warn program =
        prependSafetyWarning "OUT" program Shell

    let warnForExecution program =
        prependSafetyWarningForExecution
            "OUT"
            { program = program
              language = Shell
              dependencies = []
              timeoutType = Short
              mode = "ro"
              cwd = None
              whatToSummarize = "" }

    check "leading grep warns" (hasExactHint (warn "grep foo") hintExecutorMisuse)
    check "grep after && warns" (hasExactHint (warn "cd src && grep foo") hintExecutorMisuse)
    check "grep in pipe warns" (hasExactHint (warn "ls a | grep b") hintExecutorMisuse)
    check "stripped head pipe passes" (not (hasExactHint (warn "printf hi | head -n 1") hintExecutorMisuse))

    check
        "execution warning uses prepared program"
        (not (hasExactHint (warnForExecution "printf hi | head -n 1") hintExecutorMisuse))

    check "real head command warns" (hasExactHint (warn "head -n 1 file.txt") hintExecutorMisuse)
    check "ls after semicolon warns" (hasExactHint (warn "echo ok; ls -la") hintExecutorMisuse)
    check "prefixed path warns" (hasExactHint (warn "/usr/bin/grep foo") hintExecutorMisuse)
    check "plain echo passes" (not (hasExactHint (warn "echo hi") hintExecutorMisuse))
    check "substring inside word ignored" (not (hasExactHint (warn "echo concatenate") hintExecutorMisuse))

    check
        "non-shell language ignored"
        (not (hasExactHint (prependSafetyWarning "OUT" "grep foo" Python) hintExecutorMisuse))

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
    check "truncated response includes killed_timeout status" (truncatedResp.Contains "killed_timeout")
    check "truncated response includes Output Truncated suffix" (truncatedResp.Contains "(Output Truncated)")
    check "truncated response omits timeout_ms field" (not (truncatedResp.Contains "timeout_ms:"))
    check "truncated response omits timeout hints" (not (truncatedResp.Contains "Killed after"))
    check "truncated body excludes legacy executor suffix" (not (truncatedResp.Contains "[executor]"))
    let signaledResult = Failed("partial out", None, Some "SIGTERM")
    let signaledResp = formatToolResponse signaledResult None
    check "signaled response includes signal as status" (signaledResp.Contains "status: SIGTERM")
    check "signaled response omits legacy signal field" (not (signaledResp.Contains "signal: SIGTERM"))
    check "signaled body has no legacy suffix" (not (signaledResp.Contains "[executor]"))
    let missingResp = formatToolResponse missingResult None
    check "missing response includes status missing_executable" (missingResp.Contains "status: missing_executable")
    let summary = "SUMMARY: task succeeded"
    let summaryResp = formatToolResponse completedResult (Some summary)
    check "summary response prepends return block" (summaryResp.StartsWith "---")
    check "summary response uses summary as body" (summaryResp.Contains summary)
    check "summary response has exit_code 0" (summaryResp.Contains "exit_code: 0")

let summarizerPromptOmitsReturnValue () =
    let prompt =
        executorSummarizerPrompt "" "raw output" "shell" "echo 1" [] "short" "ro"

    check "summarizer prompt omits exit status" (not (prompt.Contains "exit status"))
    check "summarizer prompt omits non-zero" (not (prompt.ToLowerInvariant().Contains "non-zero"))
    check "summarizer empty deps yaml" (prompt.Contains "dependencies: []")

    let multiline =
        executorSummarizerPrompt "" "line1\nline2" "shell" "echo hi\necho bye" [ "dep1" ] "long" "ro"

    check "summarizer multiline program uses block field" (multiline.Contains "program: |")
    check "summarizer multiline raw output in body" (multiline.Contains "line1" && multiline.Contains "line2")

// --- formatFetchResponse ---

let formatFetchResponseAllFields () =
    let data =
        { title = Some "The Title"
          byline = Some "By Author"
          length = Some 500
          content = Some "body text" }

    let out = formatFetchResponse data
    check "front matter contains title" (out.Contains "title: The Title")
    check "front matter contains byline" (out.Contains "byline: By Author")
    check "front matter contains length" (out.Contains "length: 500")
    check "front matter contains content" (out.Contains "body text")

let formatFetchResponseOnlyTitle () =
    let data =
        { title = Some "Only Title"
          byline = None
          length = None
          content = None }

    let out = formatFetchResponse data
    check "front matter contains title" (out.Contains "title: Only Title")
    check "front matter omits byline" (not (out.Contains "byline:"))
    check "front matter omits length" (not (out.Contains "length:"))
    check "front matter omits content" (not (out.Contains "content:"))

let formatFetchResponseOnlyContent () =
    let data =
        { title = None
          byline = None
          length = None
          content = Some "just body" }

    let out = formatFetchResponse data
    check "front matter contains content" (out.Contains "just body")
    check "front matter omits title" (not (out.Contains "title:"))
    check "front matter omits byline" (not (out.Contains "byline:"))
    check "front matter omits length" (not (out.Contains "length:"))

let formatFetchResponseAllNone () =
    let data =
        { title = None
          byline = None
          length = None
          content = None }

    let out = formatFetchResponse data
    equal "formatFetchResponseAllNone returns empty" "" out
    check "front matter has no title" (not (out.Contains "title:"))
    check "front matter has no byline" (not (out.Contains "byline:"))
    check "front matter has no length" (not (out.Contains "length:"))
    check "front matter has no content" (not (out.Contains "content:"))

let formatFetchResponseEmptyTitleOmitted () =
    let data =
        { title = Some ""
          byline = None
          length = None
          content = Some "body" }

    let out = formatFetchResponse data
    check "empty title omitted" (not (out.Contains "title:"))
    check "content still present" (out.Contains "body")
