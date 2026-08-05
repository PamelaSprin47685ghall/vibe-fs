namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Kernel
open Wanxiangshu.Process
open Wanxiangshu.Session

/// Private mailbox surface: fork Executor + permit-gated Join. Never Manager Join.
module ExecutorSummarizeRuntime =

    /// Fresh FamilyRecoveryPermit per join (map/reduce mutates family → digest).
    type RequirePermit = unit -> Task<Result<FamilyRecoveryPermit, string>>

    type IExecutorRuntime =
        abstract Fork: string * AgentRole * string * string option -> Task<Result<ForkResult, string>>
        /// Agent join: require fresh permit → HostForkRuntime.JoinWithPermit. No bare Join.
        abstract JoinWithPermit: timeoutMs: int option -> Task<Result<RunCompletion, ForkError>>
        /// Cancel one owned map/reduce agent without tearing down the runtime.
        abstract CancelAgent: agentId: string -> unit

    /// AGENT-008: the Executor is internal, so its managed name is fixed here
    /// rather than chosen by a caller. This is the one legitimate place a name is
    /// derived from a role — the role is a constant, not an inference.
    let private executorAgent = ManagedAgent.nameOf AgentTier.Fast Role.Executor

    let asExecutorRuntime (runtime: HostForkRuntime) (requirePermit: RequirePermit) : IExecutorRuntime =
        { new IExecutorRuntime with
            member _.Fork(agentId, role, prompt, payload) =
                runtime.Fork(agentId, role, executorAgent, prompt, payload)

            member _.JoinWithPermit(timeoutMs) =
                task {
                    match! requirePermit () with
                    | Error msg -> return Error(ForkError.NotFound msg)
                    | Ok permit ->
                        match timeoutMs with
                        | Some ms -> return! runtime.JoinWithPermit(permit, timeoutMs = ms)
                        | None -> return! runtime.JoinWithPermit(permit)
                }

            member _.CancelAgent(agentId) = runtime.CancelAgent(agentId) }

    /// Pure ForkRuntime has no journal → cannot hold FamilyRecoveryPermit.
    /// Fail closed; do not mint a synthetic permit for mailbox-only join.
    let ofForkRuntime (_runtime: ForkRuntime) : IExecutorRuntime =
        { new IExecutorRuntime with
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

            member _.CancelAgent(_agentId) = () }
