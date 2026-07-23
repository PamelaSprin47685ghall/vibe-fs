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

let submitReviewNotNeeded () =
    equal "not needed exact" "You do not need review. Just continue with your work." submitReviewNotNeeded

let submitReviewInProgress () =
    equal "in progress exact" "A review is already in progress for this session." submitReviewInProgress

let opencodeSubmitReviewInProgress () =
    equal
        "opencode in progress exact"
        "A review is already in progress. Wait for it to finish."
        opencodeSubmitReviewInProgress

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

let reviewAlreadyActive () =
    equal
        "review already active exact"
        "With-Review Mode is already active. Submit your work via submit_review."
        reviewAlreadyActiveMessage

let subagentToolFailed () =
    let r =
        subagentToolFailed "ctx" Wanxiangshu.Kernel.ToolOutputInfoTypes.FailureReason.Aborted

    check "contains ctx" (r.Contains "ctx")
    check "contains aborted" (r.Contains "aborted")

let subagentIntentsMustBeNonEmpty () =
    equal "intents non-empty exact" "Error: `intents` must be a non-empty array." subagentIntentsMustBeNonEmpty

let run () : unit =
    muxToolRequiresWorkspaceIdX ()
    muxSubmitReviewRequiresWorkspaceId ()
    submitReviewNotNeeded ()
    submitReviewInProgress ()
    opencodeSubmitReviewInProgress ()
    toolRequiresActiveSession ()
    executorRequires ()
    executorInvalidLang ()
    reviewAlreadyActive ()
    subagentToolFailed ()
    subagentIntentsMustBeNonEmpty ()
