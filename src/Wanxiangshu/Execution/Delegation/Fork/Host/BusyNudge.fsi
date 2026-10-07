namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal

module HostForkBusyNudge =
    val profileForRun:
        journal: AgentJournal option -> run: PendingHostRun -> Result<PromptAuthority.AuthorityExecutionProfile, string>

    val send:
        sessions: ISessionHostPort ->
        journal: AgentJournal option ->
        childId: SessionId ->
        profile: PromptAuthority.AuthorityExecutionProfile ->
        directory: string option ->
        prompt: string ->
            Task<Result<unit, string>>

    val sender:
        sessions: ISessionHostPort ->
        journal: AgentJournal option ->
        directoryOf: (string -> string option) ->
        agentId: string ->
        childId: SessionId ->
        profile: PromptAuthority.AuthorityExecutionProfile ->
        prompt: string ->
            Task<Result<unit, string>>
