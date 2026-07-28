namespace Wanxiangshu.Next.Session

open System

[<RequireQualifiedAccess>]
type AgentRole =
    | Manager
    | Orchestrator
    | Coder
    | Inspector
    | Browser
    | Meditator
    | Reviewer
    | DevOps
    | Executor
    | Blogger

[<RequireQualifiedAccess>]
type AgentStatus =
    | Idle
    | Busy
    | Interrupted
    | Closed

[<RequireQualifiedAccess>]
type ForkResult =
    | Created of agentId: string
    | Nudged of agentId: string
    | NotFound of agentId: string

type ForkResult with
    member this.AgentId =
        match this with
        | ForkResult.Created id
        | ForkResult.Nudged id
        | ForkResult.NotFound id -> id

[<RequireQualifiedAccess>]
type ForkError =
    | Empty
    | NothingToJoin
    | Cancelled
    | NotFound of agentId: string

type PtyRecord =
    { PtyId: string
      AgentId: string
      Command: string
      StartedAt: DateTimeOffset }

type AgentRecord =
    { AgentId: string
      Role: AgentRole
      Status: AgentStatus
      CurrentRunId: string option
      LastCompletionStatus: string option
      HasPendingCompletion: bool
      ChildSessionId: string option }
