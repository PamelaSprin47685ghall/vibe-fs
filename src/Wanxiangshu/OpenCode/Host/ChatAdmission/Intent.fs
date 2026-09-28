namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Execution.Session.ChatExecution

[<RequireQualifiedAccess>]
module ChatAdmissionIntent =

    [<RequireQualifiedAccess>]
    type IdentityCarrierError =
        | SessionId
        | Agent
        | PromptKey

    type DecodedMessage =
        { InvalidIdentityCarrier: IdentityCarrierError option
          SessionId: SessionId option
          PhysicalUserMessageId: PhysicalUserMessageId option
          ExplicitAgent: string option
          PromptKey: PromptKey option
          IsHostCompaction: bool
          IsHostSynthetic: bool
          Text: string option }

    type DurableSnapshot =
        { Authority: PromptAuthority.PromptAuthorityProjection option }

    [<RequireQualifiedAccess>]
    type NoManagedExecutionReason =
        | UnmanagedMessage
        | AlreadyAcceptedHostMessage of PromptAuthority.ContinuationKind

    [<RequireQualifiedAccess>]
    /// DSL-class: Evidence
    type Rejection =
        | MalformedIdentityCarrier of IdentityCarrierError
        | ManagedIntentMissingSessionId
        | ManagedIntentMissingPhysicalUserMessageId
        | DurableAuthorityUnavailable
        | InvalidExplicitAgent of string
        | PromptKeyNotClaimed of PromptKey
        | AgentOwnerRootPromptNotClaimed of PromptKey * PromptAuthority.IdentitySeed
        | PromptClaimSessionMismatch of expectedSessionId: SessionId * claimedSessionId: SessionId
        | PromptClaimOriginNotAdmissible of PromptKey * PromptAuthority.PromptOrigin
        | UnknownOriginWhileActive

    type ExternalRootEvidence =
        { Key: ChatExecutionKey
          ExplicitAgent: string
          Origin: PromptAuthority.PromptOrigin
          IdentitySeed: PromptAuthority.IdentitySeed }

    type PendingPromptEvidence =
        { Key: ChatExecutionKey
          PromptKey: PromptKey
          Claim: PromptAuthority.PromptClaim
          Origin: PromptAuthority.PromptOrigin
          IdentitySeed: PromptAuthority.IdentitySeed }

    type ActiveHumanContinuationEvidence =
        { Key: ChatExecutionKey
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

    [<RequireQualifiedAccess>]
    type ManagedIntent =
        | ExternalRoot of ExternalRootEvidence
        | ActiveHumanContinuation of ActiveHumanContinuationEvidence
        | PendingPrompt of PendingPromptEvidence

    let tryManaged (decision: Decision) : ManagedIntent option =
        match decision with
        | Decision.ExternalRootIntent evidence -> Some(ManagedIntent.ExternalRoot evidence)
        | Decision.ActiveHumanContinuationIntent evidence -> Some(ManagedIntent.ActiveHumanContinuation evidence)
        | Decision.PendingPromptIntent evidence -> Some(ManagedIntent.PendingPrompt evidence)
        | Decision.NoManagedExecution _
        | Decision.HostInternal _
        | Decision.Reject _ -> None

    let ofManaged (managed: ManagedIntent) : Decision =
        match managed with
        | ManagedIntent.ExternalRoot evidence -> Decision.ExternalRootIntent evidence
        | ManagedIntent.ActiveHumanContinuation evidence -> Decision.ActiveHumanContinuationIntent evidence
        | ManagedIntent.PendingPrompt evidence -> Decision.PendingPromptIntent evidence

    let managedKey (managed: ManagedIntent) : ChatExecutionKey =
        match managed with
        | ManagedIntent.ExternalRoot evidence -> evidence.Key
        | ManagedIntent.ActiveHumanContinuation evidence -> evidence.Key
        | ManagedIntent.PendingPrompt evidence -> evidence.Key

    /// Host-internal (compaction / synthetic) classification, shared by
    /// the intent resolver and the read-side Host hooks.
    let isHostInternal (message: DecodedMessage) : bool =
        message.IsHostCompaction || message.IsHostSynthetic

    let private hostInternal (message: DecodedMessage) : Decision =
        let evidence: HostInternalEvidence =
            { SessionId = message.SessionId
              PhysicalUserMessageId = message.PhysicalUserMessageId
              Origin = PromptAuthority.PromptOrigin.HostInternal }

        Decision.HostInternal evidence

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

    let private pendingPrompt
        (key: ChatExecutionKey)
        (promptKey: PromptKey)
        (claim: PromptAuthority.PromptClaim)
        (explicitAgentOpt: string option)
        =
        let selectedParticipant =
            claim.IdentitySeed
            |> PromptAuthority.identitySeedParticipantIdentity
            |> ParticipantIdentity.selectedAgent

        match claim.SessionId = key.SessionId, claimOriginAdmissible claim.Origin, explicitAgentOpt with
        | false, _, _ -> Decision.Reject(Rejection.PromptClaimSessionMismatch(key.SessionId, claim.SessionId))
        | true, false, _ -> Decision.Reject(Rejection.PromptClaimOriginNotAdmissible(promptKey, claim.Origin))
        | true, true, Some explicitAgent when explicitAgent <> selectedParticipant ->
            Decision.Reject(Rejection.InvalidExplicitAgent explicitAgent)
        | true, true, _ ->
            Decision.PendingPromptIntent
                { Key = key
                  PromptKey = promptKey
                  Claim = claim
                  Origin = claim.Origin
                  IdentitySeed = claim.IdentitySeed }

    let private externalRoot
        (key: ChatExecutionKey)
        (explicitAgent: string)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : Decision =
        match tryRootIdentity explicitAgent, projection.ActiveLogicalRun with
        | None, _ -> Decision.Reject(Rejection.InvalidExplicitAgent explicitAgent)
        | Some identity, Some authority when ParticipantIdentity.selectedAgent identity = authority.SelectedAgent ->
            Decision.ActiveHumanContinuationIntent
                { Key = key
                  Origin = PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.HumanMessage
                  Authority = authority }
        | Some _, Some _ -> Decision.Reject Rejection.UnknownOriginWhileActive
        | Some identity, None ->

            Decision.ExternalRootIntent
                { Key = key
                  ExplicitAgent = ParticipantIdentity.selectedAgent identity
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
        (key: ChatExecutionKey)
        (message: DecodedMessage)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : Decision =
        match message.PromptKey, message.ExplicitAgent, projection.ActiveLogicalRun with
        | Some promptKey, _, Some profile when profile.AuthorityKind = PromptAuthority.RootAuthorityKind.AgentOwnerRoot ->
            Decision.Reject(Rejection.AgentOwnerRootPromptNotClaimed(promptKey, profile.IdentitySeed))
        | Some promptKey, _, _ -> Decision.Reject(Rejection.PromptKeyNotClaimed promptKey)
        | None, Some explicitAgent, _ -> externalRoot key explicitAgent projection
        | None, None, Some authority ->
            // interaction-authority-009: a wire message omitting the participant
            // field resolves against the durable active profile; the omission
            // itself is not UnknownOrigin.
            Decision.ActiveHumanContinuationIntent
                { Key = key
                  Origin = PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.HumanMessage
                  Authority = authority }
        | None, None, None -> Decision.NoManagedExecution NoManagedExecutionReason.UnmanagedMessage

    let private resolveWithProjection
        (message: DecodedMessage)
        (projection: PromptAuthority.PromptAuthorityProjection)
        (sessionId: SessionId)
        (physicalMessageId: PhysicalUserMessageId)
        : Decision =
        let key: ChatExecutionKey =
            { SessionId = sessionId
              PhysicalUserMessageId = physicalMessageId }

        match knownEvidence message projection physicalMessageId with
        | KnownEvidence.Accepted continuation ->
            Decision.NoManagedExecution(NoManagedExecutionReason.AlreadyAcceptedHostMessage continuation)
        | KnownEvidence.Pending claim -> pendingPrompt key claim.PromptKey claim message.ExplicitAgent
        | KnownEvidence.HostInternal -> hostInternal message
        | KnownEvidence.Unaccepted -> resolveUnaccepted key message projection

    let private resolveValid (message: DecodedMessage) (snapshot: DurableSnapshot) : Decision =
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

    let resolve (message: DecodedMessage) (snapshot: DurableSnapshot) : Decision =
        match message.InvalidIdentityCarrier with
        | Some carrier -> Decision.Reject(Rejection.MalformedIdentityCarrier carrier)
        | None -> resolveValid message snapshot

    let describeRejection (rejection: Rejection) : string =
        match rejection with
        | Rejection.MalformedIdentityCarrier carrier ->
            let name =
                match carrier with
                | IdentityCarrierError.SessionId -> "SessionId"
                | IdentityCarrierError.Agent -> "agent"
                | IdentityCarrierError.PromptKey -> "PromptKey"

            sprintf "Malformed identity carrier: %s" name
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
        | Rejection.PromptClaimOriginNotAdmissible(promptKey, _) ->
            sprintf "PromptKey %s has a non-admissible origin" (PromptKey.value promptKey)
        | Rejection.UnknownOriginWhileActive -> "UnknownOrigin cannot enter an active Logical Run"
