namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

module HostForkBinding =
    val managedAgent: journal: AgentJournal option -> childId: SessionId -> string option

module HostForkAgent =
    type HostForkRuntime with
        member BoundManagedAgent: childId: SessionId -> string option

        member Fork:
            agentId: string *
            role: Role *
            agent: string *
            prompt: string *
            payload: string option *
            ?firstPrompt: bool *
            ?renderedPrompt: string *
            ?ownership: HandleOwnership *
            ?deferSend: bool *
            ?byname: string *
            ?expectedToolCalls: int *
            ?preparedHandoff: PreparedDelegationHandoff ->
                Task<Result<ForkResult, string>>

        member Reuse:
            agentId: string *
            prompt: string *
            ?renderedPrompt: string *
            ?expectedToolCalls: int *
            ?preparedHandoff: PreparedDelegationHandoff ->
                Task<Result<ForkResult, string>>
