namespace Wanxiangshu.Host.Ports

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// HNI P-07: one ordered (per session), at-least-once stream of host facts.
type AttemptRef =
    { Session: SessionId
      Message: TranscriptMessageAddress option }

type ToolCallRef =
    { Session: SessionId
      CallId: ToolCallId
      Tool: string }

type RetryNotice =
    { Session: SessionId
      Attempt: int
      Failure: HostFailure }

[<RequireQualifiedAccess>]
type StreamChannel =
    | Text
    | Reasoning
    | ToolInput

type StreamChunk =
    { Session: SessionId
      Message: TranscriptMessageAddress option
      Channel: StreamChannel
      Delta: string }

[<RequireQualifiedAccess>]
type HostSignal =
    | SessionCreated of info: SessionInfo
    | SessionDeleted of session: SessionId * parent: SessionId option
    | SessionIdle of session: SessionId
    | AttemptStarted of attempt: AttemptRef
    | AttemptFinished of attempt: AttemptRef * terminal: TurnTerminal
    | ProviderRetry of notice: RetryNotice
    | ProviderFailed of session: SessionId * failure: HostFailure
    | AttemptAborted of session: SessionId * failure: HostFailure
    | ToolStarted of call: ToolCallRef
    | ToolFinished of call: ToolCallRef * result: ToolOutcome
    | Streamed of chunk: StreamChunk
    | MessageCommitted of session: SessionId * message: TranscriptMessageAddress

type ISignalSource =
    abstract Subscribe: listener: (HostSignal -> Task) -> IDisposable
