namespace Wanxiangshu.Execution.Session.ChatExecution

open System
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Persona

[<StructuralEquality; StructuralComparison>]
type ChatExecutionKey =
    { SessionId: SessionId
      PhysicalUserMessageId: PhysicalUserMessageId }

type AcceptedChatExecutionEvidence =
    { SessionId: SessionId
      LogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      AuthorityKind: PromptRootAuthorityKind
      IdentitySeed: PromptIdentitySeed
      PhysicalUserMessageId: PhysicalUserMessageId
      Origin: PromptOrigin
      EffectiveAgent: string }

[<RequireQualifiedAccess>]
module AcceptedChatExecutionEvidence =

    let private validatePublicRole identity =
        if ParticipantIdentity.role identity |> Option.isSome then
            Ok()
        else
            Error "Attempt execution evidence requires a public participant role"

    let private validateEffectiveAgentIsPresent effectiveAgent =
        if String.IsNullOrWhiteSpace effectiveAgent then
            Error "Attempt execution evidence effective agent cannot be blank"
        else
            Ok()

    let private validateEffectiveAgentBelongsToIdentity effectiveAgent identity =
        if
            effectiveAgent = ParticipantIdentity.selectedAgent identity
            || effectiveAgent = ParticipantIdentity.peerAgent identity
        then
            Ok()
        else
            Error "Attempt execution evidence effective agent is outside the authority agent pair"

    let private validateAuthorityIdentity
        (authorityKind: PromptRootAuthorityKind)
        (identitySeed: PromptIdentitySeed)
        (identity: ParticipantIdentityEvidence)
        =
        match authorityKind, identitySeed, ParticipantIdentity.origin identity with
        | PromptRootAuthorityKind.HumanRoot, RootSelection _, PersonaOrigin.ResolvedAtRoot
        | PromptRootAuthorityKind.AgentOwnerRoot, InheritedFromOwner _, PersonaOrigin.InheritedFromOwner -> Ok()
        | PromptRootAuthorityKind.HumanRoot, InheritedFromOwner _, _ ->
            Error "HumanRoot attempt execution evidence requires a root-selection identity seed"
        | PromptRootAuthorityKind.AgentOwnerRoot, RootSelection _, _ ->
            Error "AgentOwnerRoot attempt execution evidence requires an inherited owner identity seed"
        | PromptRootAuthorityKind.HumanRoot, RootSelection _, _ ->
            Error "HumanRoot attempt execution evidence requires root-resolved participant identity"
        | PromptRootAuthorityKind.AgentOwnerRoot, InheritedFromOwner _, _ ->
            Error "AgentOwnerRoot attempt execution evidence requires owner-inherited participant identity"

    let validate (evidence: AcceptedChatExecutionEvidence) : Result<unit, string> =
        let identity = PromptIdentitySeed.participantIdentity evidence.IdentitySeed

        validatePublicRole identity
        |> Result.bind (fun () -> validateEffectiveAgentIsPresent evidence.EffectiveAgent)
        |> Result.bind (fun () -> validateEffectiveAgentBelongsToIdentity evidence.EffectiveAgent identity)
        |> Result.bind (fun () -> validateAuthorityIdentity evidence.AuthorityKind evidence.IdentitySeed identity)

type ProviderStartedEvidence =
    { Accepted: AcceptedChatExecutionEvidence
      ProviderRun: ProviderRunIdentity
      RequestKind: ProviderRequestKind
      ProjectionChoice: XProjectionChoice }

[<RequireQualifiedAccess>]
module ProviderStartedEvidence =

    let validate (evidence: ProviderStartedEvidence) =
        AcceptedChatExecutionEvidence.validate evidence.Accepted
        |> Result.bind (fun () ->
            match evidence.ProjectionChoice with
            | XProjectionChoice.UseCommittedEpoch -> Ok()
            | XProjectionChoice.UsePrefixProbe _ when ProviderRequestKind.mayCarryProbe evidence.RequestKind -> Ok()
            | XProjectionChoice.UsePrefixProbe _ ->
                Error "Provider-started projection choice is incompatible with the request kind")

[<RequireQualifiedAccess>]
type ChatExecutionTerminalEvidence =
    | PreProvider of AcceptedChatExecutionEvidence
    | AfterProviderStart of ProviderStartedEvidence

[<RequireQualifiedAccess>]
type ChatExecutionTerminalDisposition =
    | Completed
    | Cancelled
    | Rejected
    | Failed

type ChatExecutionFactCases =
    | Accepted of
        {| SchemaVersion: int
           Key: ChatExecutionKey
           Evidence: AcceptedChatExecutionEvidence |}
    | ProviderStarted of
        {| SchemaVersion: int
           Key: ChatExecutionKey
           Evidence: ProviderStartedEvidence |}
    | Terminal of
        {| SchemaVersion: int
           Key: ChatExecutionKey
           Evidence: ChatExecutionTerminalEvidence
           Disposition: ChatExecutionTerminalDisposition |}
