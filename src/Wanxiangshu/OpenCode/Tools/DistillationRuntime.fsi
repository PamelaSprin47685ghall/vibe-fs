namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

module DistillationRuntime =
    type RequirePermit = unit -> Task<Result<FamilyRecoveryPermit, string>>

    type IDistillationRuntime =
        abstract Fork: string * Role * string * string option -> Task<Result<ForkResult, string>>
        abstract AwaitAgentWithPermit: agentId: string * timeoutMs: int option -> Task<Result<RunCompletion, ForkError>>
        abstract CurrentJournalRevision: unit -> JournalRevision
        abstract AwaitJournalChangeFrom: JournalRevision -> Task<JournalChange>
        abstract CancelAgent: agentId: string -> unit

    val asDistillationRuntime:
        runtime: HostForkRuntime -> journal: AgentJournal -> requirePermit: RequirePermit -> IDistillationRuntime

    val ofForkRuntime: _runtime: ForkRuntime -> IDistillationRuntime
