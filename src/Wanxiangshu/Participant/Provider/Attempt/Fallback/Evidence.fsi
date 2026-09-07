namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt

/// Durable provider failure evidence. Read-only; ProviderFailureLedger is the only writer.
module ProviderFailureEvidence =
    val currentState: sessionId: SessionId -> projection: ProjectionSet -> ProviderFailureProjection option
    val tryCurrentState: sessionId: SessionId -> projection: ProjectionSet -> ProviderFailureProjection option
    val currentBudget: sessionId: SessionId -> projection: ProjectionSet -> ProviderFailureBudget.FailureBudget option
    val mayRetry: budgetLimit: int -> sessionId: SessionId -> projection: ProjectionSet -> bool
