namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Persona

[<Sealed>]
type OwnerIdentityWitness =
    member OwnerSessionId: SessionId
    member OwnerLogicalRunId: LogicalRunId
    member OwnerAuthorityRootUserMessageId: AuthorityRootUserMessageId
    member ParticipantIdentity: ParticipantIdentityEvidence

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
    val inheritFromOwner:
        canonicalChildName: string ->
        ownerSessionId: SessionId ->
        ownerLogicalRunId: LogicalRunId ->
        ownerAuthorityRootUserMessageId: AuthorityRootUserMessageId ->
        ownerParticipantIdentity: ParticipantIdentityEvidence ->
            Result<PromptIdentitySeed, ParticipantIdentityError>

    val participantIdentity: seed: PromptIdentitySeed -> ParticipantIdentityEvidence

    val owner: seed: PromptIdentitySeed -> (SessionId * LogicalRunId * AuthorityRootUserMessageId) option

    val toInput: seed: PromptIdentitySeed -> PromptIdentitySeedInput
    val rehydrate: input: PromptIdentitySeedInput -> Result<PromptIdentitySeed, ParticipantIdentityError>
