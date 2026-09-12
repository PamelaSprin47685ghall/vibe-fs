namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt

/// Durable provider failure evidence. Read-only; ProviderFailureLedger is the only writer.
module ProviderFailureEvidence =
    val currentState: state: ProviderFailureProjection option -> ProviderFailureProjection option
    val tryCurrentState: state: ProviderFailureProjection option -> ProviderFailureProjection option
    val currentBudget: state: ProviderFailureProjection option -> ProviderFailureBudget.FailureBudget option
    val mayRetry: budgetLimit: int -> state: ProviderFailureProjection option -> bool
