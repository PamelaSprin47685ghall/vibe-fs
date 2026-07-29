namespace Wanxiangshu.Next.OpenCode

open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity

/// Public facade for the domain prompt authority model and operations.
/// Types live in Wanxiangshu.Next.Domain.PromptAuthority; run operations live
/// in Wanxiangshu.Next.Domain.PromptAuthorityRun.
[<RequireQualifiedAccess>]
module PromptAuthority =
    type RootAuthorityKind = Wanxiangshu.Next.Domain.PromptAuthority.RootAuthorityKind
    type ContinuationKind = Wanxiangshu.Next.Domain.PromptAuthority.ContinuationKind
    type PromptOrigin = Wanxiangshu.Next.Domain.PromptAuthority.PromptOrigin
    type AuthorityExecutionProfile = Wanxiangshu.Next.Domain.PromptAuthority.AuthorityExecutionProfile
    type AttemptExecutionProfile = Wanxiangshu.Next.Domain.PromptAuthority.AttemptExecutionProfile
    type PromptClaim = Wanxiangshu.Next.Domain.PromptAuthority.PromptClaim
    type PromptAuthorityProjection = Wanxiangshu.Next.Domain.PromptAuthority.PromptAuthorityProjection

    let HumanRoot = RootAuthorityKind.HumanRoot
    let AgentOwnerRoot = RootAuthorityKind.AgentOwnerRoot
    let InteractionRepair = ContinuationKind.InteractionRepair
    let ManagerGuard = ContinuationKind.ManagerGuard
    let ReviewerGuard = ContinuationKind.ReviewerGuard
    let ReviewConfirmation = ContinuationKind.ReviewConfirmation
    let BusyAgentNudge = ContinuationKind.BusyAgentNudge
    let ProviderRetryAttempt = ContinuationKind.ProviderRetryAttempt
    let HostCompactionContinue = ContinuationKind.HostCompactionContinue
    let UnknownOrigin = PromptOrigin.UnknownOrigin
    let HostInternal = PromptOrigin.HostInternal

    let empty = Wanxiangshu.Next.Domain.PromptAuthority.empty
    let newPromptKey = Wanxiangshu.Next.Domain.PromptAuthority.newPromptKey
    let originLabel = Wanxiangshu.Next.Domain.PromptAuthority.originLabel

    let tryParseContinuationKind =
        Wanxiangshu.Next.Domain.PromptAuthority.tryParseContinuationKind

    let roleLabel = Wanxiangshu.Next.Domain.PromptAuthority.roleLabel
    let tryParseRole = Wanxiangshu.Next.Domain.PromptAuthority.tryParseRole
    let tierLabel = Wanxiangshu.Next.Domain.PromptAuthority.tierLabel
    let tryParseTier = Wanxiangshu.Next.Domain.PromptAuthority.tryParseTier
    let parseAgentName = Wanxiangshu.Next.Domain.PromptAuthority.parseAgentName
    let registerAuthority = Wanxiangshu.Next.Domain.PromptAuthorityRun.registerAuthority
    let registerClaim = Wanxiangshu.Next.Domain.PromptAuthorityRun.registerClaim
    let acceptClaim = Wanxiangshu.Next.Domain.PromptAuthorityRun.acceptClaim
    let abandonClaim = Wanxiangshu.Next.Domain.PromptAuthorityRun.abandonClaim
    let repairIdentity = Wanxiangshu.Next.Domain.PromptAuthority.repairIdentity
    let tryClaimRepair = Wanxiangshu.Next.Domain.PromptAuthorityRun.tryClaimRepair

    let resolveKnownOrigin messageId promptKey hostCompact projection =
        Wanxiangshu.Next.Domain.PromptAuthorityRun.resolveKnownOrigin messageId promptKey hostCompact projection

    let stableLogicalRunId sha256 runtimeId sessionId rootUserMessageId =
        Wanxiangshu.Next.Domain.PromptAuthority.stableLogicalRunId sha256 runtimeId sessionId rootUserMessageId

    let createAuthorityRoot sha256 runtimeId sessionId rootKind messageId selectedAgentName =
        Wanxiangshu.Next.Domain.PromptAuthorityRun.createAuthorityRoot
            sha256
            runtimeId
            sessionId
            rootKind
            messageId
            selectedAgentName

    let claimAgentOwnerRoot key sessionId selectedAgentName =
        Wanxiangshu.Next.Domain.PromptAuthorityRun.claimAgentOwnerRoot key sessionId selectedAgentName

    let claimContinuation key sessionId continuation profile effectiveAgent =
        Wanxiangshu.Next.Domain.PromptAuthorityRun.claimContinuation key sessionId continuation profile effectiveAgent

    let agentPair (profile: AuthorityExecutionProfile) =
        Wanxiangshu.Next.Domain.PromptAuthority.agentPair profile

    let effectiveAgentAt (profile: AuthorityExecutionProfile) (offset: byte) =
        Wanxiangshu.Next.Domain.PromptAuthority.effectiveAgentAt profile offset

    let selectedEffectiveAgent (profile: AuthorityExecutionProfile) =
        Wanxiangshu.Next.Domain.PromptAuthority.selectedEffectiveAgent profile

    let effectiveAgentFromManaged (selected: ManagedAgent) (cursor: AgentPairCursor.FallbackCursor) : string =
        let peer = ManagedAgent.peer selected
        Wanxiangshu.Next.Domain.PromptAuthority.effectiveAgentFromManaged selected.Name peer.Name cursor
