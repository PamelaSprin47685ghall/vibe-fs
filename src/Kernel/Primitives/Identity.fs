module Wanxiangshu.Kernel.Primitives.Identity

type SessionId = private SessionId of string
type WorkspaceId = private WorkspaceId of string
type AgentId = private AgentId of string
type ToolId = private ToolId of string
type CallId = private CallId of string
type ChildId = private ChildId of string

module Id =
    let private parse (label: string) (input: string) : Result<string, string> =
        if System.String.IsNullOrEmpty input then
            Error $"{label} must be a non-empty string"
        else
            Ok input

    let sessionId input =
        parse "SessionId" input |> Result.map SessionId

    let workspaceId input =
        parse "WorkspaceId" input |> Result.map WorkspaceId

    let agentId input =
        parse "AgentId" input |> Result.map AgentId

    let toolId input =
        parse "ToolId" input |> Result.map ToolId

    let callId input =
        parse "CallId" input |> Result.map CallId

    let childId input =
        parse "ChildId" input |> Result.map ChildId

    let sessionIdValue (SessionId value) = value
    let workspaceIdValue (WorkspaceId value) = value
    let agentIdValue (AgentId value) = value
    let toolIdValue (ToolId value) = value
    let callIdValue (CallId value) = value
    let childIdValue (ChildId value) = value

    let sessionIdQuick (input: string) : SessionId = SessionId input
    let workspaceIdQuick (input: string) : WorkspaceId = WorkspaceId input
    let agentIdQuick (input: string) : AgentId = AgentId input

    let private tryId (parser: string -> Result<'id, string>) (input: string) : 'id option =
        match parser input with
        | Ok value -> Some value
        | _ -> None

    let trySessionId = tryId sessionId
    let tryWorkspaceId = tryId workspaceId
    let tryAgentId = tryId agentId
