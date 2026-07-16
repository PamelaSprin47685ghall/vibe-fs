module Wanxiangshu.Kernel.Errors.DomainError

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
