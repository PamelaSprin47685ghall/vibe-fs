module VibeFs.Kernel.Boundary

/// Concept-bound identifiers.  Each is a distinct type so the compiler refuses
/// to mix a SessionId with a WorkspaceId, even though both are strings at runtime.
/// Single-case discriminated unions give zero-cost, compiler-enforced boundaries.

type SessionId = private SessionId of string
type WorkspaceId = private WorkspaceId of string
type AgentId = private AgentId of string
type ToolId = private ToolId of string
type CallId = private CallId of string
type ChildId = private ChildId of string

module Id =

    let private parse (label: string) (input: string) : Result<string, string> =
        if System.String.IsNullOrEmpty input then Error $"{label} must be a non-empty string"
        else Ok input

    let sessionId input = parse "SessionId" input |> Result.map SessionId
    let workspaceId input = parse "WorkspaceId" input |> Result.map WorkspaceId
    let agentId input = parse "AgentId" input |> Result.map AgentId
    let toolId input = parse "ToolId" input |> Result.map ToolId
    let callId input = parse "CallId" input |> Result.map CallId
    let childId input = parse "ChildId" input |> Result.map ChildId

    let sessionIdValue (SessionId v) = v
    let workspaceIdValue (WorkspaceId v) = v
    let agentIdValue (AgentId v) = v
    let toolIdValue (ToolId v) = v
    let callIdValue (CallId v) = v
    let childIdValue (ChildId v) = v

    let sessionIdQuick (input: string) : SessionId = SessionId input
    let workspaceIdQuick (input: string) : WorkspaceId = WorkspaceId input
    let agentIdQuick (input: string) : AgentId = AgentId input

    let trySessionId (input: string) : SessionId option =
        match parse "SessionId" input with Ok v -> Some(SessionId v) | _ -> None

    let tryWorkspaceId (input: string) : WorkspaceId option =
        match parse "WorkspaceId" input with Ok v -> Some(WorkspaceId v) | _ -> None

    let tryAgentId (input: string) : AgentId option =
        match parse "AgentId" input with Ok v -> Some(AgentId v) | _ -> None
