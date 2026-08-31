namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Foundation.Identity

/// AGENT-028 / FALLBACK-014: Authority Root resolve-once; child inherits owner.
/// Persona matrix lives in PersonaCatalog; this module only binds process-local facts.
[<RequireQualifiedAccess>]
module PersonaBinding =

    let private resolveRootAgent (sessionId: SessionId) : ManagedAgent =
        SessionExecutionBinding.tryAgent sessionId
        |> Option.bind ManagedAgent.tryParse
        |> Option.defaultWith (fun () -> ManagedAgent.make AgentTier.Fast Role.Manager)

    let private resolveRootPersona (sessionId: SessionId) =
        let agent = resolveRootAgent sessionId
        PersonaCatalog.persona agent.Role agent.Tier

    /// Root / first-touch: bind once from resolved role/tier or default Manager Fast.
    let ensureRoot (sessionId: SessionId) : Result<Persona, PersonaRejection> =
        SessionPersona.bindOnce sessionId (resolveRootPersona sessionId)

    /// InternalLeaf / attached child: inherit owner SessionPersona; never re-read tier.
    let ensureInherited (ownerId: SessionId) (childId: SessionId) : Result<Persona, PersonaRejection> =
        SessionPersona.inheritFromOwner ownerId childId

    let private requireBoundInternalRootPersona (sessionId: SessionId) : Result<Persona, PersonaRejection> =
        match SessionPersona.tryGet sessionId with
        | Some inherited -> Ok inherited
        | None -> Error(PersonaRejection.OwnerPersonaMissing sessionId)

    /// Authority Root: Role × initial SelectedTier → SessionPersona (bind-once).
    let ensureFromAuthority (profile: PromptAuthority.AuthorityExecutionProfile) : Result<Persona, PersonaRejection> =
        match SessionExecutionBinding.tryParent profile.SessionId with
        | Some ownerId -> ensureInherited ownerId profile.SessionId
        | None when SessionExecutionBinding.isInternalRoot profile.SessionId ->
            requireBoundInternalRootPersona profile.SessionId
        | None ->
            PersonaCatalog.persona profile.CanonicalRole profile.SelectedTier
            |> SessionPersona.bindOnce profile.SessionId
