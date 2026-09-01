namespace Wanxiangshu.Execution.Agent

open System.Threading
open System.Threading.Tasks

module AgentProgram =
    val runAgentFlow:
        ctx: AgentContext ->
        ct: CancellationToken ->
        action: (AgentContext -> CancellationToken -> Task<'a>) ->
            Task<Result<'a, AgentError>>

    val validateSession: expectedName: string -> ctx: AgentContext -> _ct: CancellationToken -> Task<bool>
