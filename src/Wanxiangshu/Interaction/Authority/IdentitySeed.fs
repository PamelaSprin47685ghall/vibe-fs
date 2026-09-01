namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Persona

type OwnerIdentityWitness =
    private
        { StoredOwnerSessionId: SessionId
          StoredOwnerLogicalRunId: LogicalRunId
          StoredOwnerAuthorityRootUserMessageId: AuthorityRootUserMessageId
          StoredParticipantIdentity: ParticipantIdentityEvidence }

    member this.OwnerSessionId = this.StoredOwnerSessionId
    member this.OwnerLogicalRunId = this.StoredOwnerLogicalRunId

    member this.OwnerAuthorityRootUserMessageId =
        this.StoredOwnerAuthorityRootUserMessageId

    member this.ParticipantIdentity = this.StoredParticipantIdentity

type PromptIdentitySeed =
    | RootSelection of ParticipantIdentityEvidence
    | InheritedFromOwner of OwnerIdentityWitness

type OwnerIdentityWitnessInput =
    { OwnerSessionId: SessionId
      OwnerLogicalRunId: LogicalRunId
      OwnerAuthorityRootUserMessageId: AuthorityRootUserMessageId
      ParticipantIdentity: ParticipantIdentityInput }

type PromptIdentitySeedInput =
    | RootSelectionInput of ParticipantIdentityInput
    | InheritedFromOwnerInput of OwnerIdentityWitnessInput

type PromptIdentitySeedValidationError =
    | ExpectedInheritedFromOwner
    | OwnerAuthorityNotActive of SessionId
    | OwnerSessionIdMismatch of Expected: SessionId * Actual: SessionId
    | OwnerLogicalRunIdMismatch of Expected: LogicalRunId * Actual: LogicalRunId
    | OwnerAuthorityRootUserMessageIdMismatch of
        Expected: AuthorityRootUserMessageId *
        Actual: AuthorityRootUserMessageId
    | InvalidInheritedParticipantIdentity of ParticipantIdentityError

[<RequireQualifiedAccess>]
module PromptIdentitySeed =

    let inheritFromOwner
        canonicalChildName
        ownerSessionId
        ownerLogicalRunId
        ownerAuthorityRootUserMessageId
        ownerParticipantIdentity
        =
        ParticipantIdentity.inheritFromOwner canonicalChildName ownerParticipantIdentity
        |> Result.map (fun participantIdentity ->
            InheritedFromOwner
                { StoredOwnerSessionId = ownerSessionId
                  StoredOwnerLogicalRunId = ownerLogicalRunId
                  StoredOwnerAuthorityRootUserMessageId = ownerAuthorityRootUserMessageId
                  StoredParticipantIdentity = participantIdentity })

    let participantIdentity seed =
        match seed with
        | RootSelection identity -> identity
        | InheritedFromOwner witness -> witness.ParticipantIdentity

    let owner seed =
        match seed with
        | RootSelection _ -> None
        | InheritedFromOwner witness ->
            Some(witness.OwnerSessionId, witness.OwnerLogicalRunId, witness.OwnerAuthorityRootUserMessageId)

    let toInput seed =
        match seed with
        | RootSelection identity -> RootSelectionInput(ParticipantIdentity.toInput identity)
        | InheritedFromOwner witness ->
            InheritedFromOwnerInput
                { OwnerSessionId = witness.OwnerSessionId
                  OwnerLogicalRunId = witness.OwnerLogicalRunId
                  OwnerAuthorityRootUserMessageId = witness.OwnerAuthorityRootUserMessageId
                  ParticipantIdentity = ParticipantIdentity.toInput witness.ParticipantIdentity }

    let private validateInheritedOrigin identity =
        match ParticipantIdentity.origin identity with
        | PersonaOrigin.ResolvedAtRoot ->
            Error(
                ParticipantIdentityError.OriginMismatch(PersonaOrigin.InheritedFromOwner, PersonaOrigin.ResolvedAtRoot)
            )
        | PersonaOrigin.InheritedFromOwner -> Ok identity

    let rehydrate input : Result<PromptIdentitySeed, ParticipantIdentityError> =
        match input with
        | RootSelectionInput identityInput ->
            ParticipantIdentity.rehydrate None identityInput |> Result.map RootSelection
        | InheritedFromOwnerInput witnessInput ->
            ParticipantIdentity.fromInput witnessInput.ParticipantIdentity
            |> Result.bind validateInheritedOrigin
            |> Result.map (fun participantIdentity ->
                InheritedFromOwner
                    { StoredOwnerSessionId = witnessInput.OwnerSessionId
                      StoredOwnerLogicalRunId = witnessInput.OwnerLogicalRunId
                      StoredOwnerAuthorityRootUserMessageId = witnessInput.OwnerAuthorityRootUserMessageId
                      StoredParticipantIdentity = participantIdentity })
