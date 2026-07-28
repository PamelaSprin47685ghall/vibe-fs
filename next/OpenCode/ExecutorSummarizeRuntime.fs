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

    let asExecutorRuntime (runtime: HostForkRuntime) : IExecutorRuntime =
        { new IExecutorRuntime with
            member _.Fork(agentId, role, prompt) =
                runtime.Fork(agentId, role, prompt, agent = ManagedAgent.nameOf AgentTier.Fast Role.Executor)

            member _.Join() = runtime.Join() }

    let ofForkRuntime (runtime: ForkRuntime) : IExecutorRuntime =
        { new IExecutorRuntime with
            member _.Fork(agentId, role, prompt) =
                task {
                    return
                        Ok(
                            runtime.Fork(
                                agentId,
                                role,
                                prompt = prompt,
                                agent = ManagedAgent.nameOf AgentTier.Fast Role.Executor
                            )
                        )
                }

            member _.Join() = runtime.Join() }
