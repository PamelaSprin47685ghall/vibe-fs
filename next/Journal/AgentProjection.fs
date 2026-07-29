namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel.Identity

type SessionAgentProjection =
    { Companion: CompanionProjection option
      Linkage: AgentLinkageProjection option
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
          Linkage = None
          ReviewGuard = None
          ReviewRequirements = None
          Fallback = None
          PromptAuthority = None
          Effects = None }

    let empty =
        { Sessions = Map.empty
          Orchestrator = OrchestratorProjection.empty }

    let updateSession sessionId update sessions =
        let current = Map.tryFind sessionId sessions |> Option.defaultValue emptySession
        Map.add sessionId (update current) sessions

    let update sessionId update projection =
        { projection with Sessions = updateSession sessionId update projection.Sessions }
