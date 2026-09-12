namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt

/// Durable provider failure evidence. Read-only; ProviderFailureLedger is the only writer.
module ProviderFailureEvidence =

    let currentState (state: ProviderFailureProjection option) : ProviderFailureProjection option = state

    let tryCurrentState (state: ProviderFailureProjection option) : ProviderFailureProjection option =
        currentState state

    let currentBudget (state: ProviderFailureProjection option) : ProviderFailureBudget.FailureBudget option =
        currentState state |> Option.map (fun failure -> failure.Budget)

    let mayRetry (budgetLimit: int) (state: ProviderFailureProjection option) : bool =
        currentState state
        |> Option.map (ProviderFailureProjection.mayRetry budgetLimit)
        |> Option.defaultValue false
