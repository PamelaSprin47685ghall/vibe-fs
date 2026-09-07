namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt

/// Durable provider failure evidence. Read-only; ProviderFailureLedger is the only writer.
module ProviderFailureEvidence =

    let currentState (sessionId: SessionId) (projection: ProjectionSet) : ProviderFailureProjection option =
        AgentProjection.tryFind sessionId projection.AgentProjections
        |> Option.bind (fun session -> session.ProviderFailures)

    let tryCurrentState (sessionId: SessionId) (projection: ProjectionSet) : ProviderFailureProjection option =
        currentState sessionId projection

    let currentBudget (sessionId: SessionId) (projection: ProjectionSet) : ProviderFailureBudget.FailureBudget option =
        currentState sessionId projection |> Option.map (fun failure -> failure.Budget)

    let mayRetry (budgetLimit: int) (sessionId: SessionId) (projection: ProjectionSet) : bool =
        currentState sessionId projection
        |> Option.map (ProviderFailureProjection.mayRetry budgetLimit)
        |> Option.defaultValue false
