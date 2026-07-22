module Wanxiangshu.Tests.ExecutorFormatCoverageTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Runtime.ExecutorFormat
open Wanxiangshu.Runtime.SearchPrompts
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Runtime.SubagentSummarizerPrompts
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Runtime

let webApiSearchFormat () =
    let results =
        [ { title = "A"
            url = "u1"
            content = "ca" }
          { title = "B"
            url = "u2"
            content = "cb" } ]

    let formatted = formatSearchResults results
    check "search results front matter" (formatted.Contains "[[results]]")
    check "search embeds title A" (formatted.Contains "A")
    check "search embeds title B" (formatted.Contains "B")
    check "empty search" ((formatSearchResults []).Contains "results = []")

let ollamaFormat = webApiSearchFormat

let summarizerInputCap () =
    let bl (s: string) : int = s.Length

    let trunc (s: string) (maxBytes: int) : string =
        if s.Length <= maxBytes then s else s.[.. maxBytes - 1]

    let opts: ExecuteOptions =
        { command = "echo x"
          language = Shell
          dependencies = []
          timeoutType = Long
          cwd = None
          whatToSummarize = ""
          maxBytes = 8192 }

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

let private hasExactHint (msg: ToolOutputMessage) (hintText: string) =
    match msg.hint with
    | Some h -> h.Contains hintText
    | None -> false

let safetyWarning () =
    let warn program =
        prependSafetyWarning empty program Shell

    let warnForExecution program =
        prependSafetyWarningForExecution
            empty
            { command = program
              language = Shell
              dependencies = []
              timeoutType = Short
              cwd = None
              whatToSummarize = ""
              maxBytes = 8192 }

    check "leading grep warns" (hasExactHint (warn "grep foo") hintExecutorMisuse)
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
        (not (hasExactHint (prependSafetyWarning empty "grep foo" Python) hintExecutorMisuse))

let executorToolResponseFormatting () =
    let completedResult = Completed("all good", 0)
    let failedResult = Failed("boom", Some 2, None)
    let truncatedResult = Truncated("partial", Long)
    let missingResult = MissingExecutable("bash", "Error: not found")
    equal "outputFromResult completed" "all good" (outputFromResult completedResult)
    equal "outputFromResult failed" "boom" (outputFromResult failedResult)
    equal "outputFromResult truncated" "partial" (outputFromResult truncatedResult)
    equal "outputFromResult missing" "Error: not found" (outputFromResult missingResult)
    let resp = formatToolResponse completedResult None |> render
    check "response includes output payload" (resp.Contains "all good")
    check "response includes exit_code" (resp.Contains "0")
    check "response includes status completed" (resp.Contains "completed")
    let failedResp = formatToolResponse failedResult None |> render
    check "failed response includes exit_code 2" (failedResp.Contains "2")
    check "failed response includes status exit_error" (failedResp.Contains "exit_error")
    let truncatedResp = formatToolResponse truncatedResult None |> render
    check "truncated response includes killed_timeout status" (truncatedResp.Contains "killed_timeout")
    check "truncated response includes Output Truncated suffix" (truncatedResp.Contains "truncated = true")
    check "truncated response omits timeout_ms field" (not (truncatedResp.Contains "timeout_ms:"))
    check "truncated response omits timeout hints" (not (truncatedResp.Contains "Killed after"))
    check "truncated payload excludes legacy executor suffix" (not (truncatedResp.Contains "[executor]"))
    let signaledResult = Failed("partial out", None, Some "SIGTERM")
    let signaledResp = formatToolResponse signaledResult None |> render
    check "signaled response includes signal as status" (signaledResp.Contains "SIGTERM")
    check "signaled response omits legacy signal field" (not (signaledResp.Contains "signal: SIGTERM"))
    check "signaled payload has no legacy suffix" (not (signaledResp.Contains "[executor]"))
    let missingResp = formatToolResponse missingResult None |> render
    check "missing response includes status missing_executable" (missingResp.Contains "missing_executable")
    let summary = "SUMMARY: task succeeded"
    let summaryResp = formatToolResponse completedResult (Some summary) |> render
    check "summary response uses summary as stdout payload" (summaryResp.Contains summary)
    check "summary response has exit_code 0" (summaryResp.Contains "0")

let summarizerPromptOmitsReturnValue () =
    let evidence: Wanxiangshu.Kernel.Prompt.ExecutorOutputEvidence =
        { stdout = "raw output"
          stderr = None
          exitStatus = "completed"
          exitCode = Some 0
          signal = None
          truncated = false }

    let prompt = executorSummarizerPrompt "" evidence "shell" "echo 1" [] Wanxiangshu.Kernel.Prompt.TimeoutKind.Short

    check "summarizer prompt omits exit status" (not (prompt.Contains "exit status"))
    check "summarizer prompt omits non-zero" (not (prompt.ToLowerInvariant().Contains "non-zero"))
    check "summarizer empty deps toml" (prompt.Contains "dependencies = []")

    let multiline =
        executorSummarizerPrompt
            ""
            { stdout = "line1\nline2"
              stderr = None
              exitStatus = "completed"
              exitCode = Some 0
              signal = None
              truncated = false }
            "shell"
            "echo hi\necho bye"
            [ "dep1" ]
            Wanxiangshu.Kernel.Prompt.TimeoutKind.Long

    check "summarizer multiline program field" (multiline.Contains "program")
    check "summarizer multiline raw output in evidence" (multiline.Contains "line1" && multiline.Contains "line2")

// --- formatFetchResponse ---

let formatFetchResponseAllFields () =
    let data =
        { title = Some "The Title"
          byline = Some "By Author"
          length = Some 500
          content = Some "page content" }

    let out = formatFetchResponse data
    check "front matter contains title" (out.Contains "The Title")
    check "front matter contains byline" (out.Contains "By Author")
    check "front matter contains length" (out.Contains "500")
    check "front matter contains content" (out.Contains "page content")

let formatFetchResponseOnlyTitle () =
    let data =
        { title = Some "Only Title"
          byline = None
          length = None
          content = None }

    let out = formatFetchResponse data
    check "front matter contains title" (out.Contains "Only Title")
    check "front matter omits byline" (not (out.Contains "byline"))
    check "front matter omits length" (not (out.Contains "length"))

let formatFetchResponseOnlyContent () =
    let data =
        { title = None
          byline = None
          length = None
          content = Some "just content" }

    let out = formatFetchResponse data
    check "front matter contains content" (out.Contains "just content")
    check "front matter omits title" (not (out.Contains "title ="))
    check "front matter omits byline" (not (out.Contains "byline ="))

let formatFetchResponseAllNone () =
    let data =
        { title = None
          byline = None
          length = None
          content = None }

    let out = formatFetchResponse data
    check "formatFetchResponseAllNone empty document" (out = "" || not (out.Contains "title = \"\""))

let formatFetchResponseEmptyTitleOmitted () =
    let data =
        { title = Some ""
          byline = None
          length = None
          content = Some "payload" }

    let out = formatFetchResponse data
    check "empty title omitted" (not (out.Contains "title:"))
    check "content still present" (out.Contains "payload")
