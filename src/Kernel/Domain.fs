module Wanxiangshu.Kernel.Domain

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

type DomainError =
    | MessageAborted
    | ClientCancellation of source: string
    | FileSystemFault of path: string * errno: string * message: string
    | NetworkTransportFailure of url: string * statusCode: int option * body: string
    | HostProtocolMismatch of field: string * expected: string * actual: string
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
    | ClientCancellation source -> $"client cancelled: {source}"
    | FileSystemFault(path, errno, msg) -> $"file system fault: path={path}, errno={errno}: {msg}"
    | NetworkTransportFailure(url, statusCode, body) ->
        let status =
            match statusCode with
            | Some s -> string s
            | None -> "none"

        $"network transport failure: url={url}, status={status}, body={body}"
    | HostProtocolMismatch(field, expected, actual) ->
        $"host protocol mismatch: field={field}, expected={expected}, actual={actual}"
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
    | MessageAborted
    | ClientCancellation _ -> true
    | FileSystemFault _
    | NetworkTransportFailure _
    | HostProtocolMismatch _
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
    not (System.String.IsNullOrWhiteSpace message)
    && message.ToLowerInvariant().Contains("abort")

let private (|AbortError|_|) (name: string, tag: string) =
    if name = "AbortError" || name = "MessageAbortedError" || tag = "MessageAborted" then
        Some()
    else
        None

let private (|SessionBusyError|_|) (name: string, tag: string) =
    if name = "SessionBusyError" || tag = "SessionBusy" then
        Some()
    else
        None

let private (|ForegroundWaitBackgroundedError|_|) (name: string, tag: string) =
    if name = "ForegroundWaitBackgroundedError" || tag = "TaskWaitBackgrounded" then
        Some()
    else
        None

let classifyErrorLeaf (name: string) (tag: string) (message: string) : DomainError =
    match name, tag with
    | SessionBusyError -> SessionBusy
    | ForegroundWaitBackgroundedError -> TaskWaitBackgrounded
    | _ when name = "AbortError" -> ClientCancellation "AbortError"
    | _ when name = "AbortSignal" -> ClientCancellation "AbortSignal"
    | _ when tag = "MessageAborted" || name = "MessageAbortedError" -> MessageAborted
    | _ when containsAbortText message -> ClientCancellation "abort-text"
    | _ -> UnknownJsError(message)

[<RequireQualifiedAccess>]
/// Identifies a session lifecycle generation.
/// sessionGeneration increments on session create/restart, NOT on compaction.
type SessionEpoch =
    { SessionId: string
      SessionGeneration: int
      Lifecycle: string }

/// Binds every input/effect to its causal context.
/// session-start observations can use owner=None, humanTurnID=None
/// only when ObservationOptions allow it.
type CausalityContext =
    { SessionEpoch: SessionEpoch
      HumanTurnID: string option
      CancelGeneration: int
      ContextGeneration: int
      ContinuationID: string option
      RequestID: string
      Owner: string option }

[<RequireQualifiedAccess>]
/// Options for observations that may lack full identity.
type ObservationOptions =
    { AllowUnowned: bool
      AllowSessionStart: bool }

module ObservationOptions =
    let defaultOptions: ObservationOptions =
        { AllowUnowned = false
          AllowSessionStart = false }

    let sessionStart: ObservationOptions =
        { AllowUnowned = true
          AllowSessionStart = true }

module CausalityContext =

    let create
        (sessionEpoch: SessionEpoch)
        (humanTurnID: string option)
        (cancelGeneration: int)
        (contextGeneration: int)
        (continuationID: string option)
        (requestID: string)
        (owner: string option)
        : CausalityContext =
        { SessionEpoch = sessionEpoch
          HumanTurnID = humanTurnID
          CancelGeneration = cancelGeneration
          ContextGeneration = contextGeneration
          ContinuationID = continuationID
          RequestID = requestID
          Owner = owner }

    let isOwned (ctx: CausalityContext) : bool = ctx.Owner.IsSome

    let tryGetOwner (ctx: CausalityContext) : string option = ctx.Owner

    let hasHumanTurn (ctx: CausalityContext) : bool = ctx.HumanTurnID.IsSome

    let isContinuation (ctx: CausalityContext) : bool = ctx.ContinuationID.IsSome

    let sessionEpoch (ctx: CausalityContext) : SessionEpoch = ctx.SessionEpoch

    let requestId (ctx: CausalityContext) : string = ctx.RequestID

    let toSessionId (ctx: CausalityContext) : string = ctx.SessionEpoch.SessionId

/// Identity of a single human turn, carried by every Input and EffectResult.
/// Prevents old fallback, old nudge, and old model observations from
/// polluting a new turn. Only session-start / unowned observations may
/// omit this; they use CausalityContext with explicit ObservationOptions.
[<RequireQualifiedAccess>]
type TurnIdentity =
    { SessionId: string
      HumanTurnId: string
      SessionGeneration: int
      CancelGeneration: int
      UserMessageId: string }

module TurnIdentity =
    let create
        (sessionId: string)
        (humanTurnId: string)
        (sessionGeneration: int)
        (cancelGeneration: int)
        (userMessageId: string)
        : TurnIdentity =
        { SessionId = sessionId
          HumanTurnId = humanTurnId
          SessionGeneration = sessionGeneration
          CancelGeneration = cancelGeneration
          UserMessageId = userMessageId }

    let isStale (identity: TurnIdentity) (currentSessionGeneration: int) (currentCancelGeneration: int) : bool =
        identity.SessionGeneration < currentSessionGeneration
        || identity.CancelGeneration < currentCancelGeneration

    let matchesCausality (identity: TurnIdentity) (ctx: CausalityContext) : bool =
        identity.SessionId = ctx.SessionEpoch.SessionId
        && identity.HumanTurnId = (ctx.HumanTurnID |> Option.defaultValue "")
        && identity.SessionGeneration = ctx.SessionEpoch.SessionGeneration
        && identity.CancelGeneration = ctx.CancelGeneration

/// Origin classification for a terminal event, used by nudge to decide
/// whether to proceed with todo/review nudges.  PRD-06 §4.
[<RequireQualifiedAccess>]
type TerminalEventOrigin =
    | HumanTurnCompleted
    | HumanTurnAborted
    | CompactionSummaryCompleted
    | CompactionContinuationCompleted
    | FallbackContinuationCompleted
    | TitleCompleted
    | NudgeCompleted
    | ToolSubturnCompleted
    | Unknown

module TerminalEventOrigin =
    /// Only HumanTurnCompleted may enter normal todo/review nudge.
    let allowNudge (origin: TerminalEventOrigin) : bool =
        match origin with
        | TerminalEventOrigin.HumanTurnCompleted -> true
        | TerminalEventOrigin.HumanTurnAborted
        | TerminalEventOrigin.CompactionSummaryCompleted
        | TerminalEventOrigin.CompactionContinuationCompleted
        | TerminalEventOrigin.FallbackContinuationCompleted
        | TerminalEventOrigin.TitleCompleted
        | TerminalEventOrigin.NudgeCompleted
        | TerminalEventOrigin.ToolSubturnCompleted
        | TerminalEventOrigin.Unknown -> false

    let isFallback (origin: TerminalEventOrigin) : bool =
        match origin with
        | TerminalEventOrigin.FallbackContinuationCompleted -> true
        | _ -> false

    let isCompaction (origin: TerminalEventOrigin) : bool =
        match origin with
        | TerminalEventOrigin.CompactionSummaryCompleted
        | TerminalEventOrigin.CompactionContinuationCompleted -> true
        | _ -> false

    let isHuman (origin: TerminalEventOrigin) : bool =
        match origin with
        | TerminalEventOrigin.HumanTurnCompleted
        | TerminalEventOrigin.HumanTurnAborted -> true
        | _ -> false

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
    | ChildRegistered(childId, meta) ->
        { state with
            childSessions = Map.add childId meta state.childSessions }
    | ChildUnregistered childId ->
        { state with
            childSessions = Map.remove childId state.childSessions }
