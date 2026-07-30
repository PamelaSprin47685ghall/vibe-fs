namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel.Identity

/// The nine bounded projections one session owns (PERSIST-008).
///
/// Each is `option` because a session acquires state only when a fact concerning
/// it arrives. "No fact yet" and "an empty projection" are different claims and
/// must stay distinguishable — collapsing them is how a missing fact starts
/// looking like a satisfied precondition.
type SessionAgentProjection =
    {
        Companion: CompanionProjection option
        /// SSOT/12: the Companion frame sequence and what it covers. Separate from
        /// `Companion` because that record is the runtime cache's durable mirror,
        /// while this one is the frame history CTX-011 builds probe candidates from.
        Blog: BlogProjectionState option
        /// COMPANION-009: which X prefix generation is in force. Not folded into
        /// `Blog`: a squash advances the frame epoch without touching the prefix, and
        /// a reanchor retires the prefix without touching frames, so one record for
        /// both would make each change look like it moved the other.
        PrefixEpoch: ActivePrefixEpoch option
        Handles: AgentLinkageProjection option
        ReviewGuard: ReviewGuardProjection option
        ReviewRequirements: ReviewRequirementProjection option
        Fallback: FallbackProjection option
        PromptAuthority: PromptAuthority.PromptAuthorityProjection option
        Effects: DurableEffectProjection option
    }

type AgentProjectionSet =
    {
        Sessions: Map<SessionId, SessionAgentProjection>
        /// HOST-008: the Work ↔ Companion relation.
        ///
        /// Workspace-scoped rather than per-session, because the relation spans two
        /// sessions and both directions must be answerable from one keyed lookup
        /// (PERSIST-008). Held per-session, "is this id somebody's Y" would require
        /// scanning every session.
        Associations: Map<SessionId, SessionAssociation>
        Orchestrator: OrchestratorProjection
    }

/// Composition of bounded session projections. Fact routing lives in Fold.fs.
module AgentProjection =

    let emptySession =
        { Companion = None
          Blog = None
          PrefixEpoch = None
          Handles = None
          ReviewGuard = None
          ReviewRequirements = None
          Fallback = None
          PromptAuthority = None
          Effects = None }

    let empty =
        { Sessions = Map.empty
          Associations = SessionAssociationProjection.empty
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
