namespace Wanxiangshu.Composition.Durable

open System
open Wanxiangshu.Change
open Wanxiangshu.Context.Companion
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Attention
open Wanxiangshu.Interaction.Concern
open Wanxiangshu.Enforcer.InstitutionalLearning
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.OpenCode.Host
open Wanxiangshu.Participant.Provider.Attempt.Fallback

/// Durable routing vocabulary. Concrete fact families live with their semantic
/// owners; this module only joins them into the journal's outer dispatch.
module Fact =

    type RuntimeFact =
        | RuntimeStarted of
            {| RuntimeId: RuntimeId
               ProcessId: int
               StartedAt: DateTimeOffset |}

    /// One journal line for the agent domain: exactly one owned family.
    /// DSL-class: DurableFact
    [<RequireQualifiedAccess>]
    type AgentFact =
        | Prompt of PromptFactCases
        | Fallback of FallbackFactCases
        | Review of ReviewFactCases
        | Execution of ExecutionFactCases
        | Orchestrator of OrchestratorFactCases
        | Companion of CompanionFactCases
        | Context of ContextFactCases
        | Host of HostFactCases
        | Fission of FissionFactCases
        | Delegation of DelegationFactCases
        | Attention of AttentionFactCases
        | Concern of ConcernFactCases
        | InstitutionalLearning of InstitutionalLearningFactCases

    type Fact =
        | Runtime of RuntimeFact
        | Agent of AgentFact
        | ManagerLifecycle of ManagerLifecycleFact
        | MagicTodo of MagicTodoFacts.MagicTodoFact
