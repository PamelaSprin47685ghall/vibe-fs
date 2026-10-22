namespace Wanxiangshu.Host.Ports

open Wanxiangshu.Foundation.Identity

/// HNI P-03: committed session messages as neutral records.
[<RequireQualifiedAccess>]
type TranscriptRole =
    | User
    | Assistant
    | System

[<RequireQualifiedAccess>]
type ToolCallState =
    | Pending
    | Running
    | Completed
    | Failed

type ToolCallRecord =
    { CallId: ToolCallId
      Tool: string
      Input: CanonicalJsonText
      State: ToolCallState
      Output: string option }

[<RequireQualifiedAccess>]
type TranscriptPart =
    | Text of text: string
    | Reasoning of text: string
    | ToolUse of call: ToolCallRecord
    | File of mime: string * uri: string
    | Other of kind: string

type TranscriptMessage =
    { Id: TranscriptMessageAddress
      Role: TranscriptRole
      Agent: AgentRole option
      Route: ModelRoute option
      Parent: TranscriptMessageAddress option
      CreatedAt: float option
      Completed: bool
      Terminal: TurnTerminal option
      IsCompaction: bool
      Correlation: PromptKey option
      Parts: TranscriptPart list }

type ITranscriptPort =
    abstract Messages: session: SessionId -> HostResult<TranscriptMessage list>
