namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

module HostForkAgentOwner =
    val sendFirstPrompt:
        sessions: ISessionHostPort ->
        journal: AgentJournal option ->
        childId: SessionId ->
        identitySeed: PromptAuthority.IdentitySeed ->
        directory: string option ->
        prompt: string ->
            Task<Result<PromptKey, string>>

    val sendFirstPromptObserved:
        sessions: ISessionHostPort ->
        journal: AgentJournal option ->
        childId: SessionId ->
        identitySeed: PromptAuthority.IdentitySeed ->
        directory: string option ->
        prompt: string ->
        onAccepted: (PhysicalUserMessageId -> unit) ->
            Task<HostForkRunLifecycle.AgentOwnerDispatchOutcome>
