module VibeFs.Kernel.AgentRole

/// Every orchestration participant has exactly one role.  Adding a new role is
/// a compile error at every match site until it is handled — the compiler
/// remembers what the codebase forgets.
type AgentRole =
    | Orchestrator
    | Editor
    | Reviewer
    | Greper
    | Browser
    | Reverie

let allRoles: AgentRole list =
    [ Orchestrator; Editor; Reviewer; Greper; Browser; Reverie ]

let ofString (value: string) : Result<AgentRole, string> =
    match value with
    | "orchestrator" -> Ok Orchestrator
    | "editor" -> Ok Editor
    | "reviewer" -> Ok Reviewer
    | "greper" -> Ok Greper
    | "browser" -> Ok Browser
    | "reverie" -> Ok Reverie
    | other -> Error $"Invalid AgentRole: \"{other}\""

let toString (role: AgentRole) : string =
    match role with
    | Orchestrator -> "orchestrator"
    | Editor -> "editor"
    | Reviewer -> "reviewer"
    | Greper -> "greper"
    | Browser -> "browser"
    | Reverie -> "reverie"
