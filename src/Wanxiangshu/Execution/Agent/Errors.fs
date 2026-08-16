namespace Wanxiangshu.Execution.Agent

/// Domain error / context values used by direct CE programs (not a Flow AST).
/// Moved out of Foundation/Flow.fs (rotation-2): these are Execution/Agent
/// semantics, not universe-level primitives.

[<RequireQualifiedAccess>]
type AgentError =
    | HostFailure of string
    | SessionDead of string
    | InvalidFork of string
    | ParentCancelled

type AgentContext =
    { SessionId: string; AgentName: string }
