namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Foundation.Identity

/// Prompt Authority folds (docs/what/prompt.md).
///
/// Each fold takes the fact payload directly.
module PromptAuthorityLedger =
    val empty: PromptAuthority.PromptAuthorityProjection

    /// Fold an AuthorityRootAccepted payload.
    val foldAuthorityRootAccepted:
        projection: PromptAuthority.PromptAuthorityProjection ->
        payload: AuthorityRootAcceptedPayload ->
            Result<PromptAuthority.PromptAuthorityProjection, string>

    /// Complete a human manager after the road.
    val closeCompletedHumanRootManager:
        projection: PromptAuthority.PromptAuthorityProjection -> PromptAuthority.PromptAuthorityProjection

    /// PROMPT-005 `Claimed`.
    val foldPromptClaimed:
        runtimeStartCount: int ->
        projection: PromptAuthority.PromptAuthorityProjection ->
        fact:
            {| PromptKey: PromptKey
               SessionId: SessionId
               ContinuationKind: string
               LogicalRunId: LogicalRunId option
               AuthorityRootUserMessageId: AuthorityRootUserMessageId option
               IdentitySeed: PromptIdentitySeed
               PayloadDigest: string |} ->
            PromptAuthority.PromptAuthorityProjection

    /// PROMPT-005 `Submitted`: the Host call returned a transport receipt.
    val foldPromptSubmitted:
        projection: PromptAuthority.PromptAuthorityProjection ->
        fact:
            {| PromptKey: PromptKey
               SessionId: SessionId
               Receipt: TransportReceipt |} ->
            PromptAuthority.PromptAuthorityProjection

    /// PROMPT-005 `PhysicalAccepted`: a real physical message resolved the claim.
    val foldPromptPhysicalAccepted:
        projection: PromptAuthority.PromptAuthorityProjection ->
        fact:
            {| PromptKey: PromptKey
               SessionId: SessionId
               PhysicalUserMessageId: PhysicalUserMessageId |} ->
            PromptAuthority.PromptAuthorityProjection

    /// PROMPT-005 `Abandoned`. Must not change the Active Logical Run.
    val foldPromptAbandoned:
        projection: PromptAuthority.PromptAuthorityProjection ->
        fact:
            {| PromptKey: PromptKey
               SessionId: SessionId
               Reason: PromptAbandonReason |} ->
            PromptAuthority.PromptAuthorityProjection
