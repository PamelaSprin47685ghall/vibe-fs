namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Process
open Wanxiangshu.Session

/// Private mailbox surface: fork Distiller + permit-gated Join. Never Manager Join.
module DistillationRuntime =

    /// Fresh FamilyRecoveryPermit per join (map/reduce mutates family → digest).
    type RequirePermit = unit -> Task<Result<FamilyRecoveryPermit, string>>

    type IDistillationRuntime =
        abstract Fork: string * Role * string * string option -> Task<Result<ForkResult, string>>
        /// Agent join: require fresh permit → HostForkRuntime.JoinWithPermit. No bare Join.
        abstract JoinWithPermit: timeoutMs: int option -> Task<Result<RunCompletion, ForkError>>
        /// Targeted agent await: fresh permit → HostForkRuntime.AwaitAgentWithPermit.
        abstract AwaitAgentWithPermit: agentId: string * timeoutMs: int option -> Task<Result<RunCompletion, ForkError>>
        /// Revision sampled before a permit check, closing the readiness-wait race.
        abstract CurrentJournalRevision: unit -> JournalRevision
        /// Check-subscribe-recheck wait for a journal advance from the sampled revision.
        abstract AwaitJournalChangeFrom: JournalRevision -> Task<JournalChange>
        /// Cancel one owned map/reduce agent without tearing down the runtime.
        abstract CancelAgent: agentId: string -> unit

    /// AGENT-008: the Distiller is internal, so its managed name is fixed here
    /// rather than chosen by a caller. This is the one legitimate place a name is
    /// derived from a role — the role is a constant, not an inference.
    let private distillerAgent = ManagedAgent.nameOf AgentTier.Fast Role.Distiller

    let asDistillationRuntime
        (runtime: HostForkRuntime)
        (journal: AgentJournal)
        (requirePermit: RequirePermit)
        : IDistillationRuntime =
        { new IDistillationRuntime with
            member _.Fork(agentId, role, prompt, payload) =
                runtime.Fork(agentId, role, distillerAgent, prompt, payload)

            member _.JoinWithPermit(timeoutMs) =
                task {
                    match! requirePermit () with
                    | Error msg when msg.StartsWith("RECOVERY_WAITING:", System.StringComparison.Ordinal) ->
                        // FamilyWaiting: surface TimedOut (wait-not-hard-error). Hard
                        // FamilyBlocked / other permit errors → NotFound below.
                        return Error ForkError.TimedOut
                    | Error msg -> return Error(ForkError.NotFound msg)
                    | Ok permit ->
                        match timeoutMs with
                        | Some ms -> return! HostForkJoin.joinWithPermit runtime permit (Some ms)
                        | None -> return! HostForkJoin.joinWithPermit runtime permit None
                }

            member _.AwaitAgentWithPermit(agentId, timeoutMs) =
                task {
                    match! requirePermit () with
                    | Error msg when msg.StartsWith("RECOVERY_WAITING:", System.StringComparison.Ordinal) ->
                        // FamilyWaiting → TimedOut. Distillation.awaitAgentWithPermit
                        // throttle-retries within AwaitAgentTimeoutMs; NotFound is hard fail.
                        return Error ForkError.TimedOut
                    | Error msg -> return Error(ForkError.NotFound msg)
                    | Ok permit ->
                        match timeoutMs with
                        | Some ms -> return! HostForkJoin.awaitAgentWithPermit runtime permit agentId (Some ms)
                        | None -> return! HostForkJoin.awaitAgentWithPermit runtime permit agentId None
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

            member _.JoinWithPermit(_timeoutMs) =
                task {
                    return
                        Error(
                            ForkError.NotFound
                                "pure ForkRuntime has no journal; agent Join requires JoinWithPermit under FamilyRecoveryPermit"
                        )
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
