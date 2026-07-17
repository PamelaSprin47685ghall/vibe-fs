module Wanxiangshu.Tests.ToolCopyTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality

let muxToolRequiresWorkspaceIdX () =
    equal "muxToolRequiresWorkspaceId x" "x requires workspaceId" (muxToolRequiresWorkspaceId "x")

let muxSubmitReviewRequiresWorkspaceId () =
    check "submit_review name" (muxSubmitReviewRequiresWorkspaceId.Contains "submit_review")
    check "requires workspaceId" (muxSubmitReviewRequiresWorkspaceId.Contains "requires workspaceId")

let muxFuzzyFindRequiresWorkspaceId () =
    check "fuzzy_find name" (muxFuzzyFindRequiresWorkspaceId.Contains "fuzzy_find")
    check "requires workspaceId" (muxFuzzyFindRequiresWorkspaceId.Contains "requires workspaceId")

let muxFuzzyGrepRequiresWorkspaceId () =
    check "fuzzy_grep name" (muxFuzzyGrepRequiresWorkspaceId.Contains "fuzzy_grep")
    check "requires workspaceId" (muxFuzzyGrepRequiresWorkspaceId.Contains "requires workspaceId")

let submitReviewNotNeeded () =
    equal "not needed exact" "You do not need review. Just continue with your work." submitReviewNotNeeded

let submitReviewInProgress () =
    equal "in progress exact" "A review is already in progress for this session." submitReviewInProgress

let opencodeSubmitReviewInProgress () =
    equal
        "opencode in progress exact"
        "A review is already in progress. Wait for it to finish."
        opencodeSubmitReviewInProgress

let webSearchRequiredField () =
    let r = webSearchRequiredField "query"
    check "starts Web search" (r.StartsWith "Web search")
    check "contains query" (r.Contains "query")

let webFetchRequiredField () =
    let r = webFetchRequiredField "url"
    check "starts Web fetch" (r.StartsWith "Web fetch")
    check "contains url" (r.Contains "url")

let toolRequiresActiveSession () =
    let r = toolRequiresActiveSession "x"
    check "starts x" (r.StartsWith "x")
    check "requires an active session" (r.Contains "requires an active session")

let executorRequires () =
    check "executorRequiresSession contains executor" (executorRequiresSession.Contains "executor")

    check
        "executorRequiresSession contains requires an active session"
        (executorRequiresSession.Contains "requires an active session")

let executorInvalidLang () =
    check
        "executorInvalidLanguage contains expected shell"
        (executorInvalidLanguage.Contains "expected shell, python, or javascript")

let webToolFailed () =
    let r = webToolFailed "label" MessageAborted
    check "contains Web label" (r.Contains "Web label")
    check "contains aborted" (r.Contains "aborted")

let reviewAlreadyActive () =
    equal
        "review already active exact"
        "With-Review Mode is already active. Submit your work via submit_review."
        reviewAlreadyActiveMessage

let subagentToolFailed () =
    let r = subagentToolFailed "ctx" MessageAborted
    check "contains ctx" (r.Contains "ctx")
    check "contains aborted" (r.Contains "aborted")

let subagentIntentsMustBeNonEmpty () =
    equal "intents non-empty exact" "Error: `intents` must be a non-empty array." subagentIntentsMustBeNonEmpty

let run () : unit =
    muxToolRequiresWorkspaceIdX ()
    muxSubmitReviewRequiresWorkspaceId ()
    muxFuzzyFindRequiresWorkspaceId ()
    muxFuzzyGrepRequiresWorkspaceId ()
    submitReviewNotNeeded ()
    submitReviewInProgress ()
    opencodeSubmitReviewInProgress ()
    webSearchRequiredField ()
    webFetchRequiredField ()
    toolRequiresActiveSession ()
    executorRequires ()
    executorInvalidLang ()
    webToolFailed ()
    reviewAlreadyActive ()
    subagentToolFailed ()
    subagentIntentsMustBeNonEmpty ()
