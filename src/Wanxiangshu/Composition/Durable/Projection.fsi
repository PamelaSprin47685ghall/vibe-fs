namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Change
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Enforcer.InstitutionalLearning
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Attention
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Concern
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.OpenCode.Host.PairProgramming
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Participant.Provider.Attempt.Fallback

type SessionAgentProjection =
    { Companion: CompanionProjection option
      XTrace: XTraceProjectionState option
      Blog: BlogProjectionState option
      PrefixEpoch: ActivePrefixEpoch option
      Handles: AgentLinkageProjection option
      ReviewGuard: ReviewGuardProjection option
      ReviewRequirements: ReviewRequirementProjection option
      Fallback: FallbackProjection option
      PromptAuthority: PromptAuthority.PromptAuthorityProjection option
      Enforcement: EnforcementProjectionState option
      BloggerCycles: BloggerCycleProjectionState option
      ManagerLife: ManagerLifeProjection option
      Guidelines: GuidelineProjectionState option
      RequirementGrounding: RequirementGroundingProjectionState option
      TipDelivery: TipDeliveryProjectionState option
      SessionStartedAt: SessionStartedAtProjectionState option
      DelegatedToolEstimate: DelegatedToolEstimateProjectionState option }

type AgentProjectionSet =
    { Sessions: Map<SessionId, SessionAgentProjection>
      Associations: Map<SessionId, SessionAssociation>
      Orchestrator: OrchestratorProjection
      HandleByChildSession: Map<SessionId, HandleRecord>
      Fission: FissionProjectionState
      ChatExecutions: ChatExecutionProjectionState
      MagicTodo: MagicTodoProjection.MagicTodoProjectionState
      DelegationCompletedHandoffs: Map<string, int64>
      Attention: AttentionProjectionState
      Concern: ConcernProjectionState
      InstitutionalLearning: InstitutionalLearningProjectionState
      RuntimeStartCount: int }

module AgentProjection =
    val emptySession: SessionAgentProjection
    val empty: AgentProjectionSet
    val tryFind: sessionId: SessionId -> projection: AgentProjectionSet -> SessionAgentProjection option
    val mainSealedForBlogger: mainSessionId: SessionId -> projection: AgentProjectionSet -> bool

    val update:
        sessionId: SessionId ->
        apply: (SessionAgentProjection -> SessionAgentProjection) ->
        projection: AgentProjectionSet ->
        AgentProjectionSet

    val tryUpdate:
        sessionId: SessionId ->
        apply: (SessionAgentProjection -> Result<SessionAgentProjection, 'rejection>) ->
        projection: AgentProjectionSet ->
        Result<AgentProjectionSet, 'rejection>
