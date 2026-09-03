namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// Private mailbox surface: fork one Distiller + permit-gated Join. Never Manager Join.
module DistillationRuntime =

    /// Fresh FamilyRecoveryPermit per join because the Distiller is a managed child.
    type RequirePermit = unit -> Task<Result<FamilyRecoveryPermit, string>>

    type IDistillationRuntime =
        abstract Fork: string * Role * string * string option -> Task<Result<ForkResult, string>>
        /// Targeted agent await: fresh permit → HostForkRuntime.AwaitAgentWithPermit.
        abstract AwaitAgentWithPermit: agentId: string * timeoutMs: int option -> Task<Result<RunCompletion, ForkError>>
        /// Revision sampled before a permit check, closing the readiness-wait race.
        abstract CurrentJournalRevision: unit -> JournalRevision
        /// Check-subscribe-recheck wait for a journal advance from the sampled revision.
        abstract AwaitJournalChangeFrom: JournalRevision -> Task<JournalChange>
        /// Cancel the owned Distiller without tearing down the runtime.
        abstract CancelAgent: agentId: string -> unit

    /// AGENT-008: the Distiller is internal, so its managed name is fixed here
    /// rather than chosen by a caller. This is the one legitimate place a name is
    /// derived from a role — the role is a constant, not an inference.
    let private distillerAgent = ManagedAgent.nameOf Role.Distiller

    let private awaitWithTimeout runtime permit agentId timeoutMs : Task<Result<RunCompletion, ForkError>> =
        match timeoutMs with
        | Some ms -> HostForkJoin.awaitAgentWithPermit runtime permit agentId (Some ms)
        | None -> HostForkJoin.awaitAgentWithPermit runtime permit agentId None

    let asDistillationRuntime
        (runtime: HostForkRuntime)
        (journal: AgentJournal)
        (requirePermit: RequirePermit)
        : IDistillationRuntime =
        { new IDistillationRuntime with
            member _.Fork(agentId, role, prompt, payload) =
                runtime.Fork(agentId, role, distillerAgent, prompt, payload)

            member _.AwaitAgentWithPermit(agentId, timeoutMs) =
                task {
                    match! requirePermit () with
                    | Error msg when msg.StartsWith("RECOVERY_WAITING:", System.StringComparison.Ordinal) ->
                        // FamilyWaiting → TimedOut. Distillation.awaitAgentWithPermit
                        // throttle-retries within AwaitAgentTimeoutMs; NotFound is hard fail.
                        return Error ForkError.TimedOut
                    | Error msg -> return Error(ForkError.NotFound msg)
                    | Ok permit -> return! awaitWithTimeout runtime permit agentId timeoutMs
                }

            member _.CurrentJournalRevision() = AgentJournal.revision journal

            member _.AwaitJournalChangeFrom(fromRevision) =
                AgentJournal.awaitChangeFrom fromRevision journal

            member _.CancelAgent(agentId) =
                HostForkJoin.cancelAgent runtime agentId }

    /// Pure ForkRuntime has no journal → cannot hold FamilyRecoveryPermit.
    /// Fail closed; do not mint a synthetic permit for mailbox-only join.
    let ofForkRuntime (_runtime: ForkRuntime) : IDistillationRuntime =
        { new IDistillationRuntime with
            member _.Fork(_agentId, _role, _prompt, _payload) =
                task {
                    return
                        Error "ofForkRuntime cannot agent-join without FamilyRecoveryPermit; use HostForkRuntime path"
                }

            member _.AwaitAgentWithPermit(_agentId, _timeoutMs) =
                task {
                    return
                        Error(
                            ForkError.NotFound
                                "pure ForkRuntime has no journal; agent AwaitAgentWithPermit requires FamilyRecoveryPermit"
                        )
                }

            member _.CurrentJournalRevision() =
                invalidOp "pure ForkRuntime has no journal; agent await requires journal revision"

            member _.AwaitJournalChangeFrom(_fromRevision) =
                task {
                    return
                        raise (
                            InvalidOperationException
                                "pure ForkRuntime has no journal; agent await requires journal change"
                        )
                }

            member _.CancelAgent(_agentId) = () }
