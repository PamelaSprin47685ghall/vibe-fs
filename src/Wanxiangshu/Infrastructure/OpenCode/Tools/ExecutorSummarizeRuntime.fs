namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Process
open Wanxiangshu.Session

/// Private mailbox surface: fork Executor + Join. Never Manager Join.
module ExecutorSummarizeRuntime =

    type IExecutorRuntime =
        abstract Fork: string * AgentRole * string * string option -> Task<Result<ForkResult, string>>
        /// timeoutMs: None = HostForkRuntime.DefaultJoinTimeoutMs semantics via runtime.Join().
        abstract Join: timeoutMs: int option -> Task<Result<RunCompletion, ForkError>>
        /// Cancel one owned map/reduce agent without tearing down the runtime.
        abstract CancelAgent: agentId: string -> unit

    /// AGENT-008: the Executor is internal, so its managed name is fixed here
    /// rather than chosen by a caller. This is the one legitimate place a name is
    /// derived from a role — the role is a constant, not an inference.
    let private executorAgent = ManagedAgent.nameOf AgentTier.Fast Role.Executor

    let asExecutorRuntime (runtime: HostForkRuntime) : IExecutorRuntime =
        { new IExecutorRuntime with
            member _.Fork(agentId, role, prompt, payload) =
                runtime.Fork(agentId, role, executorAgent, prompt, payload)

            member _.Join(timeoutMs) =
                match timeoutMs with
                | Some ms -> runtime.Join(timeoutMs = ms)
                | None -> runtime.Join()

            member _.CancelAgent(agentId) = runtime.CancelAgent(agentId) }

    let ofForkRuntime (runtime: ForkRuntime) : IExecutorRuntime =
        { new IExecutorRuntime with
            member _.Fork(agentId, role, prompt, payload) =
                let effectivePrompt =
                    match payload with
                    | None -> prompt
                    | Some p -> prompt + "\n\n" + p

                task { return Ok(runtime.Fork(agentId, role, executorAgent, prompt = effectivePrompt)) }

            member _.Join(timeoutMs) =
                match timeoutMs with
                | Some ms -> runtime.Join(timeoutMs = ms)
                | None -> runtime.Join()

            member _.CancelAgent(agentId) = runtime.CancelAgent(agentId) }
