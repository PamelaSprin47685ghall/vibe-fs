namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable.ProjectionUpdate
open Wanxiangshu.Composition.Durable

module ProviderFailureFactFold =

    let private reject = FoldRejection.reject

    let private outcome factName projection result =
        match result with
        | Ok updated -> Ok updated
        | Error AlreadyObserved
        | Error AlreadyExhausted
        | Error DifferentRun -> Ok projection
        | Error NoActiveBudget ->
            reject factName "provider failure has no active budget: requires an accepted Authority Root"
        | Error InvalidTransition ->
            reject factName "provider failure violates validation (consecutive failure count is not valid successor)"

    let private applyFailure identity consecutiveFailureCount session =
        match session.ProviderFailures with
        | None -> Error NoActiveBudget
        | Some current ->
            ProviderFailureProjection.applyFailure identity consecutiveFailureCount current
            |> Result.map (fun updated ->
                { session with
                    ProviderFailures = Some updated })

    let private foldSuccessRecorded
        (projection: AgentProjectionSet)
        (payload:
            {| SessionId: SessionId
               LogicalRunId: LogicalRunId
               AuthorityRootUserMessageId: AuthorityRootUserMessageId
               ProviderRun: ProviderRunIdentity |})
        : Result<AgentProjectionSet, FoldRejection> =
        let currentFailure (sessionId: SessionId) (projection: AgentProjectionSet) : ProviderFailureProjection option =
            AgentProjection.tryFind sessionId projection
            |> Option.bind (fun session -> session.ProviderFailures)

        let isSupersededEpisode
            (current: ProviderFailureProjection)
            (logicalRunId: LogicalRunId)
            (authorityRoot: AuthorityRootUserMessageId)
            =
            current.LogicalRunId <> logicalRunId
            || current.AuthorityRootUserMessageId <> authorityRoot

        let applySuccessClear
            (projection: AgentProjectionSet)
            (sessionId: SessionId)
            (current: ProviderFailureProjection)
            =
            updateSession
                sessionId
                (fun s ->
                    { s with
                        ProviderFailures = Some(ProviderFailureProjection.recordSuccess current) })
                projection

        match currentFailure payload.SessionId projection with
        | None -> reject "SuccessRecorded" "success has no budget to clear: requires an accepted Authority Root"
        | Some current when isSupersededEpisode current payload.LogicalRunId payload.AuthorityRootUserMessageId ->
            Ok projection
        | Some current -> Ok(applySuccessClear projection payload.SessionId current)

    let private foldRetryExhausted
        (projection: AgentProjectionSet)
        (payload:
            {| SessionId: SessionId
               LogicalRunId: LogicalRunId
               AuthorityRootUserMessageId: AuthorityRootUserMessageId
               FinalConsecutiveFailureCount: int |})
        : Result<AgentProjectionSet, FoldRejection> =
        let currentFailure: ProviderFailureProjection option =
            AgentProjection.tryFind payload.SessionId projection
            |> Option.bind (fun session -> session.ProviderFailures)

        let isSuperseded =
            currentFailure
            |> Option.exists (fun current ->
                current.LogicalRunId <> payload.LogicalRunId
                || current.AuthorityRootUserMessageId <> payload.AuthorityRootUserMessageId)

        match currentFailure, isSuperseded with
        | None, _ -> reject "RetryExhausted" "retry exhausted has no active budget: requires an accepted Authority Root"
        | Some _, true -> Ok projection
        | Some _, false ->
            Ok(
                updateSession
                    payload.SessionId
                    (fun s ->
                        { s with
                            ProviderFailures =
                                s.ProviderFailures |> Option.map ProviderFailureProjection.applyExhausted })
                    projection
            )

    let fold
        (projection: AgentProjectionSet)
        (fact: ProviderFailureFactCases)
        : Result<AgentProjectionSet, FoldRejection> =
        match fact with
        | ProviderFailureFactCases.FailureRecorded payload ->
            let identity: FailedProviderAttemptIdentity =
                { SessionId = payload.SessionId
                  LogicalRunId = payload.LogicalRunId
                  AuthorityRootUserMessageId = payload.AuthorityRootUserMessageId
                  ProviderRun = payload.ProviderRun }

            AgentProjection.tryUpdate
                payload.SessionId
                (fun session -> applyFailure identity payload.ConsecutiveFailureCount session)
                projection
            |> outcome "FailureRecorded" projection

        | ProviderFailureFactCases.RetryExhausted payload -> foldRetryExhausted projection payload

        | ProviderFailureFactCases.SuccessRecorded payload -> foldSuccessRecorded projection payload
