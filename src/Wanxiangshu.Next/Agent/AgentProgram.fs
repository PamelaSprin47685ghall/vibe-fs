namespace Wanxiangshu.Next.Agent

open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Flow

/// Production AgentFlow program — the canonical `agent {}` builder usage.
/// This module composes manager/coder/inspector/etc. operations inside the
/// domain-specific computation expression defined in KISS-N01 §3.
module AgentProgram =

    /// Lift a plain Task into the AgentFlow.
    let private fromTask (f: AgentContext -> CancellationToken -> Task<'a>) : AgentFlow<'a> = Flow.lift f

    /// Fork a sub-agent and return once it completes.
    /// Production usage of the `agent {}` builder with fork/join semantics.
    let forkAgent
        (targetAgent: string)
        (prompt: string)
        (forkFn: string -> string -> Task<Result<string, string>>)
        : AgentFlow<string> =
        agent {
            let! handle =
                fromTask (fun _ ct ->
                    task {
                        match! forkFn targetAgent prompt with
                        | Ok h -> return h
                        | Error e -> return failwith e
                    })

            return handle
        }

    /// Run a simple agent program that validates session identity.
    let validateSession (expectedName: string) : AgentFlow<bool> =
        agent {
            let! name = fromTask (fun ctx _ct -> task { return ctx.AgentName })

            return name = expectedName
        }

    /// Execute an agent flow to completion and return the result.
    let runAgentFlow (ctx: AgentContext) (ct: CancellationToken) (flow: AgentFlow<'a>) : Task<Result<'a, AgentError>> =
        Flow.run ctx ct flow
