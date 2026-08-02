namespace Wanxiangshu.Next.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

/// Private mailbox surface: fork Executor + Join. Never Manager Join.
module ExecutorSummarizeRuntime =

    type IExecutorRuntime =
        abstract Fork: string * AgentRole * string -> Task<Result<ForkResult, string>>
        abstract Join: unit -> Task<Result<RunCompletion, ForkError>>

    /// AGENT-008: the Executor is internal, so its managed name is fixed here
    /// rather than chosen by a caller. This is the one legitimate place a name is
    /// derived from a role — the role is a constant, not an inference.
    let private executorAgent = ManagedAgent.nameOf AgentTier.Fast Role.Executor

    let asExecutorRuntime (runtime: HostForkRuntime) : IExecutorRuntime =
        { new IExecutorRuntime with
            member _.Fork(agentId, role, prompt) =
                runtime.Fork(agentId, role, executorAgent, prompt)

            member _.Join() = runtime.Join() }

    let ofForkRuntime (runtime: ForkRuntime) : IExecutorRuntime =
        { new IExecutorRuntime with
            member _.Fork(agentId, role, prompt) =
                task { return Ok(runtime.Fork(agentId, role, executorAgent, prompt = prompt)) }

            member _.Join() = runtime.Join() }
