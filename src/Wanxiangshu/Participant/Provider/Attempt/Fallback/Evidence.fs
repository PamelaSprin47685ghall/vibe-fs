namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt

/// Durable provider failure evidence. Read-only; ProviderFailureLedger is the only writer.
module ProviderFailureEvidence =

    let currentState (state: ProviderFailureProjection option) : ProviderFailureProjection option = state

    let mayRetry (budgetLimit: int) (state: ProviderFailureProjection option) : bool =
        currentState state
        |> Option.map (ProviderFailureProjection.mayRetry budgetLimit)
        |> Option.defaultValue false
