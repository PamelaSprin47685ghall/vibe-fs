namespace Wanxiangshu.Execution.Agent

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Foundation

/// Production Agent program — now expressed as functions, not a Flow AST.
///
/// ARCH-001: control flow is plain `let!/do!/match/task`, not a state machine.
/// The run helper keeps the same boundary so callers can be migrated one at a time.
module AgentProgram =

    /// Run a simple agent action with the canonical cancellation/exception mapping.
    let runAgentFlow
        (ctx: AgentContext)
        (ct: CancellationToken)
        (action: AgentContext -> CancellationToken -> Task<'a>)
        : Task<Result<'a, AgentError>> =
        task {
            try
                let! value = action ctx ct
                return Ok value
            with
            | :? OperationCanceledException when ct.IsCancellationRequested -> return Error AgentError.ParentCancelled
            | ex -> return Error(AgentError.HostFailure ex.Message)
        }

    /// Run a simple agent program that validates session identity.
    let validateSession (expectedName: string) (ctx: AgentContext) (_ct: CancellationToken) : Task<bool> =
        task { return ctx.AgentName = expectedName }
