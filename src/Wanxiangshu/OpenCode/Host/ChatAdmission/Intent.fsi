namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority

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

    val resolve: message: DecodedMessage -> snapshot: DurableSnapshot -> Decision
    val describeRejection: rejection: Rejection -> string
