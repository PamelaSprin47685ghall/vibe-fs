namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

/// AGENT-028 / FALLBACK-014: Authority Root resolve-once; child inherits owner.
/// Persona matrix lives in PersonaCatalog; this module only binds process-local facts.
[<RequireQualifiedAccess>]
module PersonaBinding =

    /// Authority Root: Role × initial SelectedTier → SessionPersona (bind-once).
    let ensureFromAuthority (profile: PromptAuthority.AuthorityExecutionProfile) : string =
        let persona = PersonaCatalog.persona profile.CanonicalRole profile.SelectedTier

        match SessionPersona.bindOnce profile.SessionId persona with
        | Ok bound -> bound
        | Error msg -> raise (InvalidOperationException msg)

    /// InternalLeaf / attached child: inherit owner SessionPersona; never re-read tier.
    let ensureInherited (ownerId: SessionId) (childId: SessionId) : string =
        let ownerPersona =
            match SessionPersona.tryGet ownerId with
            | Some persona -> persona
            | None ->
                raise (
                    InvalidOperationException(
                        sprintf
                            "owner %s has no SessionPersona; child %s cannot inherit (AGENT-028)"
                            (SessionId.value ownerId)
                            (SessionId.value childId)
                    )
                )

        match SessionPersona.inheritFromOwner ownerPersona childId with
        | Ok persona -> persona
        | Error msg -> raise (InvalidOperationException msg)
