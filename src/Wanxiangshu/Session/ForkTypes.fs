namespace Wanxiangshu.Session

open System
open Wanxiangshu.Kernel.Identity

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
    /// EXEC-009: durable HandleAbandoned — not joinable, not a hang.
    | Abandoned of agentId: string * reason: string
    | NotFound of agentId: string
    /// Join budget exhausted; durable projection still had no joinable handle.
    | TimedOut
    /// Child appeared idle/terminal on the wire but handle materialization failed.
    | TerminalMaterializationFailed of agentId: string

type PtyRecord =
    { PtyId: string
      AgentId: string
      Command: string
      StartedAt: DateTimeOffset }

type AgentRecord =
    {
        AgentId: string
        /// Managed agent name (fast-ROLE / deep-ROLE). Required; empty is refused at reuse.
        Agent: string
        Role: AgentRole
        Status: AgentStatus
        CurrentRunId: string option
        TerminalStatusLabel: string option
        CompletionCellSettled: bool
        ChildSessionId: SessionId option
    }
