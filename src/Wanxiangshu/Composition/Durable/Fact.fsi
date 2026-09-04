namespace Wanxiangshu.Composition.Durable

open System
open Wanxiangshu.Change
open Wanxiangshu.Context.Companion
open Wanxiangshu.Enforcer.InstitutionalLearning
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Attention
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Concern
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Participant.Provider.Attempt.Fallback

module Fact =
    type RuntimeFact =
        | RuntimeStarted of
            {| RuntimeId: RuntimeId
               ProcessId: int
               StartedAt: DateTimeOffset |}

    [<RequireQualifiedAccess>]
    type AgentFact =
        | Prompt of PromptFactCases
        | Fallback of FallbackFactCases
        | Relay of RelayFactCases
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
        | ChatExecution of ChatExecutionFactCases

    type Fact =
        | Runtime of RuntimeFact
        | Agent of AgentFact
        | MagicTodo of MagicTodoFacts.MagicTodoFact
