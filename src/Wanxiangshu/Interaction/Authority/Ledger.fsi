namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation.Identity

module PromptAuthorityLedger =
    val empty: PromptAuthority.PromptAuthorityProjection

    val foldAuthorityRootAccepted:
        projection: PromptAuthority.PromptAuthorityProjection ->
        payload: AuthorityRootAcceptedPayload ->
            Result<PromptAuthority.PromptAuthorityProjection, string>

    val closeCompletedHumanRootManager:
        projection: PromptAuthority.PromptAuthorityProjection -> PromptAuthority.PromptAuthorityProjection

    val foldPromptClaimed:
        runtimeStartCount: int ->
        projection: PromptAuthority.PromptAuthorityProjection ->
        fact:
            {| PromptKey: PromptKey
               SessionId: SessionId
               ContinuationKind: string
               LogicalRunId: LogicalRunId option
               AuthorityRootUserMessageId: AuthorityRootUserMessageId option
               EffectiveAgent: string option
               IdentitySeed: PromptIdentitySeed
               PayloadDigest: string |} ->
            PromptAuthority.PromptAuthorityProjection

    val foldPromptSubmitted:
        projection: PromptAuthority.PromptAuthorityProjection ->
        fact:
            {| PromptKey: PromptKey
               SessionId: SessionId
               Receipt: TransportReceipt |} ->
            PromptAuthority.PromptAuthorityProjection

    val foldPromptPhysicalAccepted:
        projection: PromptAuthority.PromptAuthorityProjection ->
        fact:
            {| PromptKey: PromptKey
               SessionId: SessionId
               PhysicalUserMessageId: PhysicalUserMessageId |} ->
            PromptAuthority.PromptAuthorityProjection

    val foldPromptAbandoned:
        projection: PromptAuthority.PromptAuthorityProjection ->
        fact:
            {| PromptKey: PromptKey
               SessionId: SessionId
               Reason: PromptAbandonReason |} ->
            PromptAuthority.PromptAuthorityProjection

    val projectionFor:
        sessionId: SessionId -> agentProjections: AgentProjectionSet -> PromptAuthority.PromptAuthorityProjection option

    val activeProfile:
        sessionId: SessionId -> agentProjections: AgentProjectionSet -> PromptAuthority.AuthorityExecutionProfile option

    val lastAuthorityProfile:
        sessionId: SessionId -> agentProjections: AgentProjectionSet -> PromptAuthority.AuthorityExecutionProfile option

    val pendingClaim:
        sessionId: SessionId ->
        promptKey: PromptKey ->
        agentProjections: AgentProjectionSet ->
            PromptAuthority.PromptClaim option

    [<RequireQualifiedAccess>]
    type DispatchStatus =
        | Accepted of evidence: PromptAuthority.AcceptedDispatch
        | Pending
        | Dispatchable

    val pendingDispatchClaim:
        sessionId: SessionId ->
        payloadDigest: string ->
        agentProjections: AgentProjectionSet ->
            PromptAuthority.PromptClaim option

    val acceptedDispatchForPhysicalMessage:
        sessionId: SessionId ->
        physicalUserMessageId: PhysicalUserMessageId ->
        agentProjections: AgentProjectionSet ->
            PromptAuthority.AcceptedDispatch option

    val dispatchStatusFor:
        sessionId: SessionId -> payloadDigest: string -> agentProjections: AgentProjectionSet -> DispatchStatus
