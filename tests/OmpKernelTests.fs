module Wanxiangshu.Tests.OmpKernelTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Kernel.SubagentPrompts
open Wanxiangshu.Kernel.WebFetchGuard

let private mainSessionActive =
    [| "read"; "edit"; "write"; "find"; "fuzzy_find"; "fuzzy_grep"; "lsp"; "browser"; "search"; "glob"
       "ast_edit"; "ast_grep"; "bash"; "coder"; "investigator"; "meditator"; "executor"; "executor_wait"
       "executor_abort"; "submit_review"; "return_reviewer"; "websearch"; "webfetch"; "todowrite" |]

let filterOmpMainSessionTools () =
    let filtered = filterOmpMainSessionActiveTools mainSessionActive
    let set = Set.ofArray filtered
    for name in
        [| "read"; "coder"; "investigator"; "meditator"; "executor"; "submit_review"; "websearch"; "webfetch"
           "todowrite" |] do
        check ("main keeps " + name) (set.Contains name)
    for name in
        [| "find"; "edit"; "write"; "lsp"; "fuzzy_find"; "fuzzy_grep"; "executor_wait"; "executor_abort"
           "return_reviewer"; "search"; "glob"; "ast_edit"; "ast_grep"; "browser"; "bash" |] do
        check ("main strips " + name) (not (set.Contains name))

let validateFetchUrlBlocksPrivate () =
    let blocked =
        [| "http://localhost/"
           "http://127.0.0.1/"
           "http://0.0.0.0/"
           "http://[::1]/"
           "http://10.0.0.1/"
           "http://192.168.1.1/"
           "http://169.254.169.254/" |]
    for url in blocked do
        check ("ssrf block " + url) (match validateFetchUrl url with Error _ -> true | Ok _ -> false)
    check "ssrf allow public https" (match validateFetchUrl "https://example.com/path" with Ok _ -> true | _ -> false)

let reviewInstructionsCanonicalVerdictTool () =
    check "reviewInstructions has return_reviewer" (reviewInstructions.Contains "return_reviewer")
    check "reviewInstructions PASS token" (reviewInstructions.Contains "verdict")
    check "reviewInstructions no submit_review_result" (not (reviewInstructions.Contains "submit_review_result"))
    let initial = buildOmpReviewInitialPrompt "report body" [ "src/a.fs" ] (Some "fix login")
    check "initial prompt has return_reviewer" (initial.Contains "return_reviewer")
    check "initial prompt no submit_review_result" (not (initial.Contains "submit_review_result"))
    check "initial prompt has change report" (initial.Contains "=== Change Report ===")
    check "initial prompt has affected files" (initial.Contains "src/a.fs")

let executorSummarizerPromptCarriesWhatToSummarize () =
    let marker = "summarize exit codes and stderr only"
    let prompt =
        executorSummarizerPrompt marker "raw" "shell" "echo 1" [] "omp-runner" "rw"
    check "prompt has what_to_summarize field" (prompt.Contains "what_to_summarize:")
    check "prompt embeds summarize intent" (prompt.Contains marker)