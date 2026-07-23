module Wanxiangshu.Tests.OmpKernelTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Runtime.ReviewPrompts
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Runtime.SubagentSummarizerPrompts

let private mainSessionActive =
    [| "read"
       "edit"
       "write"
       "find"
       "lsp"
       "browser"
       "search"
       "glob"
       "ast_edit"
       "ast_grep"
       "bash"
       "coder"
       "inspector"
       "meditator"
       "browser"
       "executor"
       "submit_review"
       "return_reviewer"
       "todowrite" |]

let filterOmpMainSessionTools () =
    let filtered = filterOmpMainSessionActiveTools mainSessionActive
    let set = Set.ofArray filtered

    for name in
        [| "read"
           "coder"
           "inspector"
           "meditator"
           "executor"
           "submit_review"
           "todowrite" |] do
        check ("main keeps " + name) (set.Contains name)

    for name in
        [| "find"
           "edit"
           "write"
           "lsp"
           "return_reviewer"
           "search"
           "glob"
           "ast_edit"
           "ast_grep"
           "browser"
           "bash" |] do
        check ("main strips " + name) (not (set.Contains name))


let reviewInstructionsCanonicalVerdictTool () =
    check "reviewInstructions has return_reviewer" (reviewInstructions.Contains "return_reviewer")
    check "reviewInstructions PERFECT token" (reviewInstructions.Contains "PERFECT")
    check "reviewInstructions no submit_review_result" (not (reviewInstructions.Contains "submit_review_result"))

    let initial =
        buildOmpReviewInitialPrompt "report content" [ "src/a.fs" ] (Some "fix login")

    check "initial prompt has return_reviewer" (initial.Contains "return_reviewer")
    check "initial prompt no submit_review_result" (not (initial.Contains "submit_review_result"))
    check "initial prompt has change report evidence" (initial.Contains "report content")
    check "initial prompt has affected files" (initial.Contains "src/a.fs")
    check "initial prompt no markdown section headers" (not (initial.Contains "=== "))

let executorSummarizerPromptCarriesWhatToSummarize () =
    let marker = "summarize exit codes and stderr only"

    let evidence: Wanxiangshu.Kernel.Prompt.ExecutorOutputEvidence =
        { stdout = "raw"
          stderr = None
          exitStatus = "completed"
          exitCode = Some 0
          signal = None
          truncated = false }

    let prompt =
        executorSummarizerPrompt marker evidence "shell" "echo 1" [] Wanxiangshu.Kernel.Prompt.TimeoutKind.Long

    check "prompt has objective" (prompt.Contains "objective =")
    check "prompt embeds summarize intent" (prompt.Contains marker)
