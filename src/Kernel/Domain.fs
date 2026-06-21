module VibeFs.Kernel.Domain

open VibeFs.Kernel.Dyn

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

let private containsAbortText (message: string) : bool =
    not (System.String.IsNullOrWhiteSpace message) && message.ToLowerInvariant().Contains("abort")

let private (|AbortError|_|) (name: string, tag: string) =
    if name = "AbortError" || name = "MessageAbortedError" || tag = "MessageAborted" then Some () else None

let private (|SessionBusyError|_|) (name: string, tag: string) =
    if name = "SessionBusyError" || tag = "SessionBusy" then Some () else None

let private (|ForegroundWaitBackgroundedError|_|) (name: string, tag: string) =
    if name = "ForegroundWaitBackgroundedError" || tag = "TaskWaitBackgrounded" then Some () else None

let nowMs () : int64 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

let translateJsError (error: obj) : DomainError =
    let rec classify (value: obj) (seen: obj list) =
        if Dyn.isNullish value then SystemPanic "Null error context"
        elif List.exists (fun seenObj -> obj.ReferenceEquals(value, seenObj)) seen then SystemPanic "Cyclic error context"
        elif Dyn.typeIs value "string" then
            let message = string value
            if containsAbortText message then MessageAborted else UnknownJsError(message)
        else
            let seenNext = value :: seen
            let name = Dyn.str value "name"
            let tag = Dyn.str value "_tag"
            match name, tag with
            | AbortError -> MessageAborted
            | SessionBusyError -> SessionBusy
            | ForegroundWaitBackgroundedError -> TaskWaitBackgrounded
            | _ ->
                let nested = Dyn.get value "error"
                if not (Dyn.isNullish nested) then classify nested seenNext
                else
                    let data = Dyn.get value "data"
                    if not (Dyn.isNullish data) && Dyn.typeIs data "object" then classify data seenNext
                    else
                        let cause = Dyn.get value "cause"
                        if not (Dyn.isNullish cause) then classify cause seenNext
                        else
                            let message = Dyn.str value "message"
                            if containsAbortText message then MessageAborted else UnknownJsError(message)
    classify error []

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
