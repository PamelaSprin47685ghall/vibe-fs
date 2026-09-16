namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host

/// Retired Distiller runtime stub (PROC-013 / DISTILL-014).
module DistillationRuntime =

    type RequirePermit = unit -> Task<Result<FamilyRecoveryPermit, string>>

    type IDistillationRuntime =
        abstract Fork: string * Role * string * string option -> Task<Result<ForkResult, string>>
        abstract AwaitAgentWithPermit: agentId: string * timeoutMs: int option -> Task<Result<RunCompletion, ForkError>>
        abstract CurrentJournalRevision: unit -> JournalRevision
        abstract AwaitJournalChangeFrom: JournalRevision -> Task<JournalChange>
        abstract CancelAgent: agentId: string -> unit

    let asDistillationRuntime
        (_runtime: HostForkRuntime)
        (_journal: AgentJournal)
        (_requirePermit: RequirePermit)
        : IDistillationRuntime =
        { new IDistillationRuntime with
            member _.Fork(_agentId, _role, _prompt, _payload) =
                task { return Error "Distiller runtime is deleted" }

            member _.AwaitAgentWithPermit(_agentId, _timeoutMs) =
                task { return Error(ForkError.NotFound "Distiller runtime is deleted") }

            member _.CurrentJournalRevision() = JournalRevision.initial

            member _.AwaitJournalChangeFrom(_fromRevision) =
                task { return Unchecked.defaultof<JournalChange> }

            member _.CancelAgent(_agentId) = () }

    let ofForkRuntime (_runtime: ForkRuntime) : IDistillationRuntime =
        { new IDistillationRuntime with
            member _.Fork(_agentId, _role, _prompt, _payload) =
                task { return Error "Distiller runtime is deleted" }

            member _.AwaitAgentWithPermit(_agentId, _timeoutMs) =
                task { return Error(ForkError.NotFound "Distiller runtime is deleted") }

            member _.CurrentJournalRevision() = JournalRevision.initial

            member _.AwaitJournalChangeFrom(_fromRevision) =
                task { return Unchecked.defaultof<JournalChange> }

            member _.CancelAgent(_agentId) = () }
