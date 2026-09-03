namespace Wanxiangshu.Participant.Persona

open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type PersonaOrigin =
    | ResolvedAtRoot
    | InheritedFromOwner

[<RequireQualifiedAccess>]
type ParticipantIdentityError =
    | BlankParticipantName
    | LegacyParticipantName of string
    | MalformedParticipantName of string
    | UnknownParticipantName of string
    | UnsupportedPersonaCatalogVersion of int
    | RoleMismatch of Expected: Role option * Actual: Role option
    | BlankPersona
    | PersonaMismatch of Expected: string * Actual: string
    | OriginMismatch of Expected: PersonaOrigin * Actual: PersonaOrigin
    | OwnerRequired
    | OwnerPersonaMismatch of Expected: string * Actual: string
    | OwnerCatalogVersionMismatch of Expected: int * Actual: int
    | LegacyRoleMismatch of Expected: string * Actual: string
    | UnsupportedLegacyAuthorityKind of string
    | UnprovableLegacyAuthorityIdentity of string

type ParticipantIdentityInput =
    { SelectedAgent: string
      Role: Role option
      Persona: string
      PersonaCatalogVersion: int
      Origin: PersonaOrigin }

type LegacyAuthorityRootIdentityV1Input =
    { AuthorityKind: string
      SelectedAgent: string
      CanonicalRole: string }

type ParticipantIdentity
type ParticipantIdentityEvidence

[<RequireQualifiedAccess>]
module ParticipantIdentity =
    val selectedAgent: ParticipantIdentityEvidence -> string
    val role: ParticipantIdentityEvidence -> Role option
    val roleLabel: ParticipantIdentityEvidence -> string
    val peerAgent: ParticipantIdentityEvidence -> string
    val persona: ParticipantIdentityEvidence -> string
    val personaCatalogVersion: ParticipantIdentityEvidence -> int
    val origin: ParticipantIdentityEvidence -> PersonaOrigin
    val toInput: ParticipantIdentityEvidence -> ParticipantIdentityInput
    val resolveAtRoot: string -> Result<ParticipantIdentityEvidence, ParticipantIdentityError>

    val inheritFromOwner:
        string -> ParticipantIdentityEvidence -> Result<ParticipantIdentityEvidence, ParticipantIdentityError>

    val rehydrate:
        ParticipantIdentityEvidence option ->
        ParticipantIdentityInput ->
            Result<ParticipantIdentityEvidence, ParticipantIdentityError>

    val fromInput: ParticipantIdentityInput -> Result<ParticipantIdentityEvidence, ParticipantIdentityError>
    val legacyAgentOwnerRootUnprovableMessage: string

    val upgradeLegacyV1Root:
        LegacyAuthorityRootIdentityV1Input -> Result<ParticipantIdentityEvidence, ParticipantIdentityError>
