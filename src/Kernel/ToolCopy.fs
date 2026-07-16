module Wanxiangshu.Kernel.ToolCopy

open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.ToolResult

let muxToolRequiresWorkspaceId (title: string) : string = $"{title} requires workspaceId"

let muxSubmitReviewRequiresWorkspaceId: string =
    muxToolRequiresWorkspaceId "submit_review"

let muxFuzzyFindRequiresWorkspaceId: string =
    muxToolRequiresWorkspaceId "fuzzy_find"

let muxFuzzyGrepRequiresWorkspaceId: string =
    muxToolRequiresWorkspaceId "fuzzy_grep"

let submitReviewNotNeeded: string =
    "You do not need review. Just continue with your work."

let submitReviewInProgress: string =
    "A review is already in progress for this session."

let opencodeSubmitReviewInProgress: string =
    "A review is already in progress. Wait for it to finish."

let webSearchRequiredField (field: string) : string =
    wireEncodeToolError "Web search" (InvalidIntent("websearch", field, "required"))

let webFetchRequiredField (field: string) : string =
    wireEncodeToolError "Web fetch" (InvalidIntent("webfetch", field, "required"))

let toolRequiresActiveSession (toolName: string) : string =
    wireEncodeToolError toolName (InvalidIntent(toolName, "session", "requires an active session"))

let executorRequiresSession: string = toolRequiresActiveSession "executor"

let executorInvalidLanguage: string =
    wireEncodeToolError "Executor" (InvalidIntent("executor", "language", "expected shell, python, or javascript"))

let webToolFailed (label: string) (error: DomainError) : string =
    wireEncodeToolError $"Web {label}" error

let reviewAlreadyActiveMessage: string =
    "With-Review Mode is already active. Submit your work via submit_review."

let preReviewPassedMessage (task: string) : string =
    $"Pre-review passed. Task \"{task}\" already meets all criteria — no changes needed."

let preReviewCouldNotComplete: string = "Pre-review could not complete."

let withReviewPreReviewFeedbackHeader: string = "=== Pre-review Feedback ==="

let subagentToolFailed (context: string) (error: DomainError) : string = wireEncodeToolError context error

let subagentIntentsMustBeNonEmpty: string =
    "Error: `intents` must be a non-empty array."
