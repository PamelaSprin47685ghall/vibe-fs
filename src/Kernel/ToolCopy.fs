module VibeFs.Kernel.ToolCopy

open VibeFs.Kernel.Domain

let muxToolRequiresWorkspaceId (title: string) : string =
    $"{title} requires workspaceId"

let muxSubmitReviewRequiresWorkspaceId : string =
    muxToolRequiresWorkspaceId "submit_review"

let muxFuzzyFindRequiresWorkspaceId : string =
    muxToolRequiresWorkspaceId "fuzzy_find"

let muxFuzzyGrepRequiresWorkspaceId : string =
    muxToolRequiresWorkspaceId "fuzzy_grep"

let submitReviewNotNeeded : string =
    "You do not need review. Just continue with your work."

let submitReviewInProgress : string =
    "A review is already in progress for this session."

let opencodeSubmitReviewInProgress : string =
    "A review is already in progress. Wait for it to finish."

let private contextFailed (context: string) (error: DomainError) : string =
    $"{context} failed: {formatDomainError error}"

let webSearchRequiredField (field: string) : string =
    contextFailed "Web search" (InvalidIntent ("websearch", field, "required"))

let webFetchRequiredField (field: string) : string =
    contextFailed "Web fetch" (InvalidIntent ("webfetch", field, "required"))

let toolRequiresActiveSession (toolName: string) : string =
    contextFailed toolName (InvalidIntent (toolName, "session", "requires an active session"))

let executorRequiresSession : string =
    toolRequiresActiveSession "executor"

let executorInvalidLanguage : string =
    contextFailed "Executor" (InvalidIntent ("executor", "language", "expected shell, python, or javascript"))

let webToolFailed (label: string) (error: DomainError) : string =
    $"Web {label} failed: {formatDomainError error}"