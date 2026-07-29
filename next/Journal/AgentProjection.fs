namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel.Identity

/// The seven bounded projections one session owns (PERSIST-008).
///
/// Each is `option` because a session acquires state only when a fact concerning
/// it arrives. "No fact yet" and "an empty projection" are different claims and
/// must stay distinguishable — collapsing them is how a missing fact starts
/// looking like a satisfied precondition.
type SessionAgentProjection =
    { Companion: CompanionProjection option
      Handles: AgentLinkageProjection option
      ReviewGuard: ReviewGuardProjection option
      ReviewRequirements: ReviewRequirementProjection option
      Fallback: FallbackProjection option
      PromptAuthority: PromptAuthority.PromptAuthorityProjection option
      Effects: DurableEffectProjection option }

type AgentProjectionSet =
    { Sessions: Map<SessionId, SessionAgentProjection>
      Orchestrator: OrchestratorProjection }

/// Composition of bounded session projections. Fact routing lives in Fold.fs.
module AgentProjection =

    let emptySession =
        { Companion = None
          Handles = None
          ReviewGuard = None
          ReviewRequirements = None
          Fallback = None
          PromptAuthority = None
          Effects = None }

    let empty =
        { Sessions = Map.empty
          Orchestrator = OrchestratorProjection.empty }

    let tryFind (sessionId: SessionId) (projection: AgentProjectionSet) =
        Map.tryFind sessionId projection.Sessions

    let private sessionOrEmpty sessionId projection =
        Map.tryFind sessionId projection.Sessions |> Option.defaultValue emptySession

    let update sessionId apply projection =
        { projection with
            Sessions = Map.add sessionId (apply (sessionOrEmpty sessionId projection)) projection.Sessions }

    /// Update one session when the change itself may be refused.
    ///
    /// Threads the rejection out rather than swallowing it. A projection that
    /// silently ignores an invalid fact cannot fail closed, and FALLBACK-007's
    /// modulo-4 validation and REVIEW-003's causal proof both require exactly
    /// that.
    let tryUpdate
        (sessionId: SessionId)
        (apply: SessionAgentProjection -> Result<SessionAgentProjection, 'rejection>)
        (projection: AgentProjectionSet)
        : Result<AgentProjectionSet, 'rejection> =
        sessionOrEmpty sessionId projection
        |> apply
        |> Result.map (fun session ->
            { projection with
                Sessions = Map.add sessionId session projection.Sessions })
