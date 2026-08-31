namespace Wanxiangshu.Participant.Persona

open System.Collections.Generic
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type PersonaRejection =
    | ConflictingPersona of sessionId: SessionId * existing: Persona * attempted: Persona
    | OwnerPersonaMissing of ownerSessionId: SessionId

[<RequireQualifiedAccess>]
module PersonaRejection =

    let render (rejection: PersonaRejection) : string =
        match rejection with
        | PersonaRejection.ConflictingPersona(sessionId, existing, attempted) ->
            sprintf
                "Session '%s' persona conflict: existing '%s', attempted '%s'."
                (SessionId.value sessionId)
                (Persona.render existing)
                (Persona.render attempted)
        | PersonaRejection.OwnerPersonaMissing ownerSessionId ->
            sprintf "Owner session '%s' has no bound persona." (SessionId.value ownerSessionId)

module SessionPersona =

    let private gate = obj ()
    // DSL-MUTABLE: resource — per-session persona registry
    let private bySession = Dictionary<SessionId, Persona>()

    let clearAllForTests () = lock gate (fun () -> bySession.Clear())

    let tryGet (sessionId: SessionId) : Persona option =
        lock gate (fun () ->
            match bySession.TryGetValue sessionId with
            | true, persona -> Some persona
            | false, _ -> None)

    let drop (sessionId: SessionId) : unit =
        lock gate (fun () -> bySession.Remove sessionId |> ignore)

    let bindOnce (sessionId: SessionId) (persona: Persona) : Result<Persona, PersonaRejection> =
        lock gate (fun () ->
            match bySession.TryGetValue sessionId with
            | true, existing when existing = persona -> Ok existing
            | true, existing -> Error(PersonaRejection.ConflictingPersona(sessionId, existing, persona))
            | false, _ ->
                bySession.[sessionId] <- persona
                Ok persona)

    let inheritFromOwner (ownerSessionId: SessionId) (childSessionId: SessionId) : Result<Persona, PersonaRejection> =
        lock gate (fun () ->
            match bySession.TryGetValue ownerSessionId, bySession.TryGetValue childSessionId with
            | (false, _), _ -> Error(PersonaRejection.OwnerPersonaMissing ownerSessionId)
            | (true, ownerPersona), (true, existing) when existing = ownerPersona -> Ok existing
            | (true, ownerPersona), (true, existing) ->
                Error(PersonaRejection.ConflictingPersona(childSessionId, existing, ownerPersona))
            | (true, ownerPersona), (false, _) ->
                bySession.[childSessionId] <- ownerPersona
                Ok ownerPersona)
