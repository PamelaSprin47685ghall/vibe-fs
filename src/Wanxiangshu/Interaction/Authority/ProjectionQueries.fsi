namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Query layer translating an AgentProjectionSet to PromptAuthority facts with
/// durable-level readonly views. This must be durable-composition because it
/// has complete knowledge of AgentProjectionSet.
module PromptAuthorityProjectionQueries =

    /// Returns the authority projection associated with the given session.
    val projectionFor: sessionId: SessionId -> agentProjections: AgentProjectionSet -> PromptAuthority.PromptAuthorityProjection option

    /// Returns the authority projection for the fission-owner of this session.
    val activeProfile: sessionId: SessionId -> agentProjections: AgentProjectionSet -> PromptAuthority.AuthorityExecutionProfile option

    /// Returns the last known authority projection for the fission-owner of this session.
    val lastAuthorityProfile: sessionId: SessionId -> agentProjections: AgentProjectionSet -> PromptAuthority.AuthorityExecutionProfile option

    /// Returns the pending claim for the given session and prompt key.
    val pendingClaim: sessionId: SessionId -> promptKey: PromptKey -> agentProjections: AgentProjectionSet -> PromptAuthority.PromptClaim option

    [<RequireQualifiedAccess>]
    type DispatchStatus =
        | Accepted of evidence: PromptAuthority.AcceptedDispatch
        | Pending
        | Dispatchable

    /// Finds a pending dispatch claim matching the given payload digest.
    val pendingDispatchClaim: sessionId: SessionId -> payloadDigest: string -> agentProjections: AgentProjectionSet -> PromptAuthority.PromptClaim option

    /// Finds the accepted dispatch for the physical message.
    val acceptedDispatchForPhysicalMessage: sessionId: SessionId -> physicalUserMessageId: PhysicalUserMessageId -> agentProjections: AgentProjectionSet -> PromptAuthority.AcceptedDispatch option

    /// Durable status of a dispatch.
    val dispatchStatusFor: sessionId: SessionId -> payloadDigest: string -> agentProjections: AgentProjectionSet -> DispatchStatus

    /// Issues an inherited identity seed for a managed child.
    val issueCurrentOwnerIdentitySeed: agentProjections: AgentProjectionSet -> ownerSessionId: SessionId -> childAgent: string -> Result<PromptAuthority.IdentitySeed, string>
