namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Persona

[<RequireQualifiedAccess>]
module ChatAdmissionIntent =

    type DecodedMessage =
        { SessionId: SessionId option
          PhysicalUserMessageId: PhysicalUserMessageId option
          ExplicitAgent: string option
          PromptKey: PromptKey option
          IsHostCompaction: bool
          IsHostSynthetic: bool
          Text: string option }

    type DurableSnapshot =
        { Authority: PromptAuthority.PromptAuthorityProjection option }

    type ExecutionKey =
        { SessionId: SessionId
          PhysicalUserMessageId: PhysicalUserMessageId }

    [<RequireQualifiedAccess>]
    type NoManagedExecutionReason =
        | UnmanagedMessage
        | AlreadyAcceptedHostMessage of PromptAuthority.ContinuationKind

    [<RequireQualifiedAccess>]
    /// DSL-class: Evidence
    type Rejection =
        | ManagedIntentMissingSessionId
        | ManagedIntentMissingPhysicalUserMessageId
        | DurableAuthorityUnavailable
        | InvalidExplicitAgent of string
        | PromptKeyNotClaimed of PromptKey
        | AgentOwnerRootPromptNotClaimed of PromptKey * PromptAuthority.IdentitySeed
        | PromptClaimSessionMismatch of expectedSessionId: SessionId * claimedSessionId: SessionId
        | PromptClaimMissingManagedEffectiveAgent of PromptKey
        | PromptClaimOriginNotAdmissible of PromptKey * PromptAuthority.PromptOrigin
        | UnknownOriginWhileActive

    type ExternalRootEvidence =
        { Key: ExecutionKey
          ExplicitAgent: string
          EffectiveAgent: string
          Origin: PromptAuthority.PromptOrigin
          IdentitySeed: PromptAuthority.IdentitySeed }

    type PendingPromptEvidence =
        { Key: ExecutionKey
          PromptKey: PromptKey
          Claim: PromptAuthority.PromptClaim
          EffectiveAgent: string
          Origin: PromptAuthority.PromptOrigin
          IdentitySeed: PromptAuthority.IdentitySeed }

    type ActiveHumanContinuationEvidence =
        { Key: ExecutionKey
          EffectiveAgent: string
          Origin: PromptAuthority.PromptOrigin
          Authority: PromptAuthority.AuthorityExecutionProfile }

    type HostInternalEvidence =
        { SessionId: SessionId option
          PhysicalUserMessageId: PhysicalUserMessageId option
          Origin: PromptAuthority.PromptOrigin }

    [<RequireQualifiedAccess>]
    type Decision =
        | NoManagedExecution of NoManagedExecutionReason
        | ExternalRootIntent of ExternalRootEvidence
        | ActiveHumanContinuationIntent of ActiveHumanContinuationEvidence
        | PendingPromptIntent of PendingPromptEvidence
        | HostInternal of HostInternalEvidence
        | Reject of Rejection

    let private isHostInternal (message: DecodedMessage) : bool =
        message.IsHostCompaction || message.IsHostSynthetic

    let private hostInternal (message: DecodedMessage) : Decision =
        let evidence: HostInternalEvidence =
            { SessionId = message.SessionId
              PhysicalUserMessageId = message.PhysicalUserMessageId
              Origin = PromptAuthority.PromptOrigin.HostInternal }

        Decision.HostInternal evidence

    let private tryManagedAgent (value: string option) =
        value
        |> Option.map (fun agent -> agent.Trim())
        |> Option.filter (fun agent ->
            not (String.IsNullOrWhiteSpace agent)
            && (ManagedAgent.requiredNames |> List.contains agent))

    let private tryRootIdentity (value: string) =
        ParticipantIdentity.resolveAtRoot value
        |> Result.toOption
        |> Option.filter (fun identity -> ParticipantIdentity.role identity |> Option.isSome)

    let private rejectMissingPhysical
        (message: DecodedMessage)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : bool =
        message.PromptKey.IsSome
        || message.ExplicitAgent.IsSome
        || projection.ActiveLogicalRun.IsSome

    let private claimOriginAdmissible (origin: PromptAuthority.PromptOrigin) : bool =
        match origin with
        | PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot
        | PromptAuthority.PromptOrigin.Continuation _ -> true
        | PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.HumanRoot
        | PromptAuthority.PromptOrigin.HostInternal
        | PromptAuthority.PromptOrigin.UnknownOrigin -> false

    let private pendingPrompt (key: ExecutionKey) (promptKey: PromptKey) (claim: PromptAuthority.PromptClaim) =
        match
            claim.SessionId = key.SessionId, claimOriginAdmissible claim.Origin, tryManagedAgent claim.EffectiveAgent
        with
        | false, _, _ -> Decision.Reject(Rejection.PromptClaimSessionMismatch(key.SessionId, claim.SessionId))
        | true, false, _ -> Decision.Reject(Rejection.PromptClaimOriginNotAdmissible(promptKey, claim.Origin))
        | true, true, None -> Decision.Reject(Rejection.PromptClaimMissingManagedEffectiveAgent promptKey)
        | true, true, Some effectiveAgent ->
            Decision.PendingPromptIntent
                { Key = key
                  PromptKey = promptKey
                  Claim = claim
                  EffectiveAgent = effectiveAgent
                  Origin = claim.Origin
                  IdentitySeed = claim.IdentitySeed }

    let private externalRoot
        (key: ExecutionKey)
        (explicitAgent: string)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : Decision =
        match tryRootIdentity explicitAgent, projection.ActiveLogicalRun with
        | None, _ -> Decision.Reject(Rejection.InvalidExplicitAgent explicitAgent)
        | Some identity, Some authority when ParticipantIdentity.selectedAgent identity = authority.SelectedAgent ->
            Decision.ActiveHumanContinuationIntent
                { Key = key
                  EffectiveAgent = authority.SelectedAgent
                  Origin = PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.HumanMessage
                  Authority = authority }
        | Some _, Some _ -> Decision.Reject Rejection.UnknownOriginWhileActive
        | Some identity, None ->
            let effectiveAgent = ParticipantIdentity.selectedAgent identity

            Decision.ExternalRootIntent
                { Key = key
                  ExplicitAgent = effectiveAgent
                  EffectiveAgent = effectiveAgent
                  Origin = PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.HumanRoot
                  IdentitySeed = PromptAuthority.IdentitySeed.RootSelection identity }

    [<RequireQualifiedAccess>]
    type private KnownEvidence =
        | Accepted of PromptAuthority.ContinuationKind
        | Pending of PromptAuthority.PromptClaim
        | HostInternal
        | Unaccepted

    let private knownEvidence
        (message: DecodedMessage)
        (projection: PromptAuthority.PromptAuthorityProjection)
        (physicalMessageId: PhysicalUserMessageId)
        : KnownEvidence =
        let accepted = Map.tryFind physicalMessageId projection.AcceptedContinuationIds

        let pending =
            message.PromptKey
            |> Option.bind (fun promptKey -> Map.tryFind promptKey projection.PendingClaims)

        match accepted, pending, isHostInternal message with
        | Some continuation, _, _ -> KnownEvidence.Accepted continuation
        | None, Some claim, _ -> KnownEvidence.Pending claim
        | None, None, true -> KnownEvidence.HostInternal
        | None, None, false -> KnownEvidence.Unaccepted

    let private resolveUnaccepted
        (key: ExecutionKey)
        (message: DecodedMessage)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : Decision =
        match message.PromptKey, message.ExplicitAgent, projection.ActiveLogicalRun with
        | Some promptKey, _, Some profile when profile.AuthorityKind = PromptAuthority.RootAuthorityKind.AgentOwnerRoot ->
            Decision.Reject(Rejection.AgentOwnerRootPromptNotClaimed(promptKey, profile.IdentitySeed))
        | Some promptKey, _, _ -> Decision.Reject(Rejection.PromptKeyNotClaimed promptKey)
        | None, Some explicitAgent, _ -> externalRoot key explicitAgent projection
        | None, None, Some _ -> Decision.Reject Rejection.UnknownOriginWhileActive
        | None, None, None -> Decision.NoManagedExecution NoManagedExecutionReason.UnmanagedMessage

    let private resolveWithProjection
        (message: DecodedMessage)
        (projection: PromptAuthority.PromptAuthorityProjection)
        (sessionId: SessionId)
        (physicalMessageId: PhysicalUserMessageId)
        : Decision =
        let key: ExecutionKey =
            { SessionId = sessionId
              PhysicalUserMessageId = physicalMessageId }

        match knownEvidence message projection physicalMessageId with
        | KnownEvidence.Accepted continuation ->
            Decision.NoManagedExecution(NoManagedExecutionReason.AlreadyAcceptedHostMessage continuation)
        | KnownEvidence.Pending claim -> pendingPrompt key claim.PromptKey claim
        | KnownEvidence.HostInternal -> hostInternal message
        | KnownEvidence.Unaccepted -> resolveUnaccepted key message projection

    let resolve (message: DecodedMessage) (snapshot: DurableSnapshot) : Decision =
        match message.SessionId, snapshot.Authority, message.PhysicalUserMessageId with
        | None, _, _ when isHostInternal message -> hostInternal message
        | None, _, _ when message.PromptKey.IsSome || message.ExplicitAgent.IsSome ->
            Decision.Reject Rejection.ManagedIntentMissingSessionId
        | None, _, _ -> Decision.NoManagedExecution NoManagedExecutionReason.UnmanagedMessage
        | Some _, None, _ when isHostInternal message -> hostInternal message
        | Some _, None, _ when message.PromptKey.IsSome || message.ExplicitAgent.IsSome ->
            Decision.Reject Rejection.DurableAuthorityUnavailable
        | Some _, None, _ -> Decision.NoManagedExecution NoManagedExecutionReason.UnmanagedMessage
        | Some _, Some projection, None when rejectMissingPhysical message projection ->
            Decision.Reject Rejection.ManagedIntentMissingPhysicalUserMessageId
        | Some _, Some _, None when isHostInternal message -> hostInternal message
        | Some _, Some _, None -> Decision.NoManagedExecution NoManagedExecutionReason.UnmanagedMessage
        | Some sessionId, Some projection, Some physicalMessageId ->
            resolveWithProjection message projection sessionId physicalMessageId

    let describeRejection (rejection: Rejection) : string =
        match rejection with
        | Rejection.ManagedIntentMissingSessionId -> "Managed chat intent requires a SessionId"
        | Rejection.ManagedIntentMissingPhysicalUserMessageId ->
            "Managed chat intent requires an exact PhysicalUserMessageId"
        | Rejection.DurableAuthorityUnavailable -> "Managed chat intent requires a durable authority snapshot"
        | Rejection.InvalidExplicitAgent agent ->
            sprintf "HumanRoot participant identity resolution failed for %s" agent
        | Rejection.PromptKeyNotClaimed promptKey ->
            sprintf "PromptKey %s is not an exact pending claim" (PromptKey.value promptKey)
        | Rejection.AgentOwnerRootPromptNotClaimed(promptKey, _) ->
            sprintf "AgentOwnerRoot PromptKey %s is not an exact pending claim" (PromptKey.value promptKey)
        | Rejection.PromptClaimSessionMismatch(expectedSessionId, claimedSessionId) ->
            sprintf
                "Prompt claim session mismatch: expected %s, claimed %s"
                (SessionId.value expectedSessionId)
                (SessionId.value claimedSessionId)
        | Rejection.PromptClaimMissingManagedEffectiveAgent promptKey ->
            sprintf "PromptKey %s has no managed EffectiveAgent" (PromptKey.value promptKey)
        | Rejection.PromptClaimOriginNotAdmissible(promptKey, _) ->
            sprintf "PromptKey %s has a non-admissible origin" (PromptKey.value promptKey)
        | Rejection.UnknownOriginWhileActive -> "UnknownOrigin cannot enter an active Logical Run"
