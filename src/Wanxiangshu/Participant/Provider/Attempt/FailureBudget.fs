namespace Wanxiangshu.Participant.Provider.Attempt

open System
open Wanxiangshu.Foundation.Identity

/// Exact identity of one physical provider attempt (used for failure deduplication).
type FailedProviderAttemptIdentity =
    { SessionId: SessionId
      LogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      ProviderRun: ProviderRunIdentity }

module FailedProviderAttemptIdentity =
    /// Stable string form for set membership in a projection.
    let dedupeKey (identity: FailedProviderAttemptIdentity) =
        String.Join(
            "\u001f",
            [| SessionId.value identity.SessionId
               LogicalRunId.value identity.LogicalRunId
               AuthorityRootUserMessageId.value identity.AuthorityRootUserMessageId
               ProviderRunIdentity.value identity.ProviderRun |]
        )

/// Pure provider failure budget. No Host, Journal or Fable dependency.
///
/// Bounded consecutive failure budget tracking.
/// Exact failure deduplication + consecutive failure budget increment + exhaustion check.
[<RequireQualifiedAccess>]
module ProviderFailureBudget =

    type FailureBudget = { ConsecutiveFailureCount: int }

    [<Literal>]
    let DefaultBudget = 12

    /// What the controller may do after a failure has been recorded.
    type Verdict =
        /// Budget remains; the next automatic attempt may be issued.
        | MayRetry of FailureBudget
        /// Budget consumed. Issue no further automatic physical request.
        | Exhausted of FailureBudget

    let initial: FailureBudget = { ConsecutiveFailureCount = 0 }

    /// On failure: budget is consumed by one.
    let recordFailure (budget: FailureBudget) : FailureBudget =
        { ConsecutiveFailureCount = budget.ConsecutiveFailureCount + 1 }

    /// On business-main success: reset the failure budget.
    let recordSuccess (budget: FailureBudget) : FailureBudget =
        ignore budget
        { ConsecutiveFailureCount = 0 }

    /// Judgement happens after the failure is recorded.
    let verdict (budgetLimit: int) (budget: FailureBudget) : Verdict =
        if budget.ConsecutiveFailureCount >= budgetLimit then
            Exhausted budget
        else
            MayRetry budget

    let forNewAuthorityRoot: FailureBudget = initial

    /// Validation: consecutive failure count advances exactly once from the folded state.
    /// A prior success is already represented by a folded count of zero.
    let isValidRecord (previousCount: int) (nextCount: int) : bool = nextCount = previousCount + 1

    /// The identity used to deduplicate one failed attempt.
    let attemptIdentity
        (sessionId: SessionId)
        (logicalRunId: LogicalRunId)
        (authorityRoot: AuthorityRootUserMessageId)
        (providerRun: ProviderRunIdentity)
        : FailedProviderAttemptIdentity =
        { SessionId = sessionId
          LogicalRunId = logicalRunId
          AuthorityRootUserMessageId = authorityRoot
          ProviderRun = providerRun }
