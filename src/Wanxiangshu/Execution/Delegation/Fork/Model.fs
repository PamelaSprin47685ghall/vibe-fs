namespace Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

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
    /// A second concurrent join is rejected instead of waiting on a wake that another join consumed.
    | JoinInProgress
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
        Role: Role
        Status: AgentStatus
        CurrentRunId: string option
        TerminalStatusLabel: string option
        CompletionCellSettled: bool
        ChildSessionId: SessionId option
    }
