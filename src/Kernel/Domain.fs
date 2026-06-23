module VibeFs.Kernel.Domain

type SessionId = private SessionId of string
type WorkspaceId = private WorkspaceId of string
type AgentId = private AgentId of string
type ToolId = private ToolId of string
type CallId = private CallId of string
type ChildId = private ChildId of string

module Id =
    let private parse (label: string) (input: string) : Result<string, string> =
        if System.String.IsNullOrEmpty input then Error $"{label} must be a non-empty string" else Ok input

    let sessionId input = parse "SessionId" input |> Result.map SessionId
    let workspaceId input = parse "WorkspaceId" input |> Result.map WorkspaceId
    let agentId input = parse "AgentId" input |> Result.map AgentId
    let toolId input = parse "ToolId" input |> Result.map ToolId
    let callId input = parse "CallId" input |> Result.map CallId
    let childId input = parse "ChildId" input |> Result.map ChildId

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
        match parser input with Ok value -> Some value | _ -> None

    let trySessionId = tryId sessionId
    let tryWorkspaceId = tryId workspaceId
    let tryAgentId = tryId agentId

type DomainError =
    | MessageAborted
    | SessionBusy
    | TaskWaitBackgrounded
    | ExecutorExecutableMissing of executable: string
    | ParseError of context: string * detail: string
    | ToolNotPermitted of agent: string * tool: string
    | InvalidIntent of tool: string * field: string * detail: string
    | UpstreamTimeout of seconds: int
    | UpstreamRefused of reason: string
    | SystemPanic of message: string
    | UnknownJsError of message: string

let formatDomainError (error: DomainError) : string =
    match error with
    | MessageAborted -> "aborted"
    | SessionBusy -> "session busy"
    | TaskWaitBackgrounded -> "task wait backgrounded"
    | ExecutorExecutableMissing exe -> $"executable not found: {exe}"
    | ParseError(ctx, detail) -> $"parse error in {ctx}: {detail}"
    | ToolNotPermitted(agent, tool) -> $"tool '{tool}' not permitted for agent '{agent}'"
    | InvalidIntent(tool, field, detail) -> $"invalid {field} for tool '{tool}': {detail}"
    | UpstreamTimeout seconds -> $"upstream timeout after {seconds}s"
    | UpstreamRefused reason -> $"upstream refused: {reason}"
    | SystemPanic message -> $"system panic: {message}"
    | UnknownJsError message -> message

let isAbort (error: DomainError) : bool =
    match error with
    | MessageAborted -> true
    | SessionBusy
    | TaskWaitBackgrounded
    | ExecutorExecutableMissing _
    | ParseError _
    | ToolNotPermitted _
    | InvalidIntent _
    | UpstreamTimeout _
    | UpstreamRefused _
    | SystemPanic _
    | UnknownJsError _ -> false

let containsAbortText (message: string) : bool =
    not (System.String.IsNullOrWhiteSpace message) && message.ToLowerInvariant().Contains("abort")

let private (|AbortError|_|) (name: string, tag: string) =
    if name = "AbortError" || name = "MessageAbortedError" || tag = "MessageAborted" then Some () else None

let private (|SessionBusyError|_|) (name: string, tag: string) =
    if name = "SessionBusyError" || tag = "SessionBusy" then Some () else None

let private (|ForegroundWaitBackgroundedError|_|) (name: string, tag: string) =
    if name = "ForegroundWaitBackgroundedError" || tag = "TaskWaitBackgrounded" then Some () else None

let classifyErrorLeaf (name: string) (tag: string) (message: string) : DomainError =
    match name, tag with
    | AbortError -> MessageAborted
    | SessionBusyError -> SessionBusy
    | ForegroundWaitBackgroundedError -> TaskWaitBackgrounded
    | _ ->
        if containsAbortText message then MessageAborted else UnknownJsError(message)

let nowMs () : int64 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

type ChildSessionMeta =
    { agent: string
      parentSessionId: SessionId option }

type WorkspaceState =
    { childSessions: Map<ChildId, ChildSessionMeta> }

type WorkspaceEvent =
    | ChildRegistered of childId: ChildId * meta: ChildSessionMeta
    | ChildUnregistered of childId: ChildId

let empty: WorkspaceState = { childSessions = Map.empty }

let reduce (state: WorkspaceState) (event: WorkspaceEvent) : WorkspaceState =
    match event with
    | ChildRegistered(childId, meta) -> { state with childSessions = Map.add childId meta state.childSessions }
    | ChildUnregistered childId -> { state with childSessions = Map.remove childId state.childSessions }
