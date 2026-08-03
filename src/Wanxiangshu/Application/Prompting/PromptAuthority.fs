namespace Wanxiangshu.OpenCode

open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// Public facade for the domain prompt authority model and operations.
/// Types live in Wanxiangshu.Domain.PromptAuthority; run operations live
/// in Wanxiangshu.Domain.PromptAuthorityRun.
[<RequireQualifiedAccess>]
module PromptAuthority =
    type RootAuthorityKind = Wanxiangshu.Domain.PromptAuthority.RootAuthorityKind
    type ContinuationKind = Wanxiangshu.Domain.PromptAuthority.ContinuationKind
    type PromptOrigin = Wanxiangshu.Domain.PromptAuthority.PromptOrigin
    type AuthorityExecutionProfile = Wanxiangshu.Domain.PromptAuthority.AuthorityExecutionProfile
    type AttemptExecutionProfile = Wanxiangshu.Domain.PromptAuthority.AttemptExecutionProfile
    type PromptClaim = Wanxiangshu.Domain.PromptAuthority.PromptClaim
    type PromptAuthorityProjection = Wanxiangshu.Domain.PromptAuthority.PromptAuthorityProjection

    let HumanRoot = RootAuthorityKind.HumanRoot
    let AgentOwnerRoot = RootAuthorityKind.AgentOwnerRoot
    let InteractionRepair = ContinuationKind.InteractionRepair
    let ManagerGuard = ContinuationKind.ManagerGuard
    let ReviewerGuard = ContinuationKind.ReviewerGuard
    let ReviewConfirmation = ContinuationKind.ReviewConfirmation
    let BusyAgentNudge = ContinuationKind.BusyAgentNudge
    let ProviderRetryAttempt = ContinuationKind.ProviderRetryAttempt
    let UnknownOrigin = PromptOrigin.UnknownOrigin
    let HostInternal = PromptOrigin.HostInternal

    let empty = Wanxiangshu.Domain.PromptAuthority.empty
    let originLabel = Wanxiangshu.Domain.PromptAuthority.originLabel

    let tryParseContinuationKind =
        Wanxiangshu.Domain.PromptAuthority.tryParseContinuationKind

    let roleLabel = Wanxiangshu.Domain.PromptAuthority.roleLabel
    let tryParseRole = Wanxiangshu.Domain.PromptAuthority.tryParseRole
    let tierLabel = Wanxiangshu.Domain.PromptAuthority.tierLabel
    let tryParseTier = Wanxiangshu.Domain.PromptAuthority.tryParseTier
    let parseAgentName = Wanxiangshu.Domain.PromptAuthority.parseAgentName
    let registerAuthority = Wanxiangshu.Domain.PromptAuthorityRun.registerAuthority
    let registerClaim = Wanxiangshu.Domain.PromptAuthorityRun.registerClaim
    let acceptClaim = Wanxiangshu.Domain.PromptAuthorityRun.acceptClaim
    let abandonClaim = Wanxiangshu.Domain.PromptAuthorityRun.abandonClaim

    let repairPayloadDigest = Wanxiangshu.Domain.PromptAuthority.repairPayloadDigest

    let repairAlreadyClaimed = Wanxiangshu.Domain.PromptAuthority.repairAlreadyClaimed

    let resolveKnownOrigin messageId promptKey hostCompact projection =
        Wanxiangshu.Domain.PromptAuthorityRun.resolveKnownOrigin messageId promptKey hostCompact projection

    let stableLogicalRunId sha256 runtimeId sessionId rootUserMessageId =
        Wanxiangshu.Domain.PromptAuthority.stableLogicalRunId sha256 runtimeId sessionId rootUserMessageId

    let createAuthorityRoot sha256 runtimeId sessionId rootKind messageId selectedAgentName =
        Wanxiangshu.Domain.PromptAuthorityRun.createAuthorityRoot
            sha256
            runtimeId
            sessionId
            rootKind
            messageId
            selectedAgentName

    let claimAgentOwnerRoot key sessionId selectedAgentName =
        Wanxiangshu.Domain.PromptAuthorityRun.claimAgentOwnerRoot key sessionId selectedAgentName

    let claimContinuation key sessionId continuation profile effectiveAgent =
        Wanxiangshu.Domain.PromptAuthorityRun.claimContinuation key sessionId continuation profile effectiveAgent

    let agentPair (profile: AuthorityExecutionProfile) =
        Wanxiangshu.Domain.PromptAuthority.agentPair profile

    let effectiveAgentAt (profile: AuthorityExecutionProfile) (offset: byte) =
        Wanxiangshu.Domain.PromptAuthority.effectiveAgentAt profile offset
