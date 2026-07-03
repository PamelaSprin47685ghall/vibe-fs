module Wanxiangshu.Shell.Contracts

open Wanxiangshu.Kernel.Domain

/// Boundary DTO carrying host context into kernel-bound pipelines.
type HostContext = {
    SessionId: string
    WorkspaceRoot: string
    HostName: string
    AgentRole: string option
}

/// Boundary DTO for a single tool-call frame reaching the executor.
type ToolCallRequest = {
    CallId: string
    ToolName: string
    Args: obj
}

/// Host-reported session lifecycle events consumed by dispatchers.
type HookEvent =
    | SessionIdle
    | SessionBusy
    | SessionError of DomainError
    | TurnEnd
