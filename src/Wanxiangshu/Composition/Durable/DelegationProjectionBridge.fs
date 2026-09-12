namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Foundation

module DelegationProjectionBridge =

    let private sessionState (projection: AgentProjectionSet) (sessionId: SessionId) : DelegationSessionState option =
        Map.tryFind sessionId projection.Sessions
        |> Option.map (fun s ->
            { Handles = s.Handles
              ToolEstimate = s.DelegatedToolEstimate })

    let private handoffFrontier (projection: AgentProjectionSet) (key: string) : int64 option =
        Map.tryFind key projection.DelegationCompletedHandoffs

    let private applyChange (projection: AgentProjectionSet) (change: DelegationProjectionChange) : AgentProjectionSet =
        match change with
        | ReplaceSessionState(sessionId, state) ->
            let current =
                Map.tryFind sessionId projection.Sessions
                |> Option.defaultValue AgentProjection.emptySession

            let updated =
                { current with
                    Handles = state.Handles
                    DelegatedToolEstimate = state.ToolEstimate }

            { projection with
                Sessions = Map.add sessionId updated projection.Sessions }

        | IndexChildHandle(childSessionId, record) ->
            { projection with
                HandleByChildSession = Map.add childSessionId record projection.HandleByChildSession }

        | MoveHandoffFrontier(key, endExclusive) ->
            { projection with
                DelegationCompletedHandoffs = Map.add key endExclusive projection.DelegationCompletedHandoffs }

        | TerminatedChildHandle childSessionId ->
            let session =
                Map.tryFind childSessionId projection.Sessions
                |> Option.defaultValue AgentProjection.emptySession

            let updatedAuthority =
                session.PromptAuthority
                |> Option.bind (fun current ->
                    current.ActiveLogicalRun
                    |> Option.bind (fun active ->
                        PromptAuthorityRun.closeCompletedAgentOwnerChildWork
                            active.LogicalRunId
                            active.AuthorityRootUserMessageId
                            current
                        |> Result.toOption))
                |> Option.orElse session.PromptAuthority

            { projection with
                Sessions =
                    Map.add
                        childSessionId
                        { session with
                            PromptAuthority = updatedAuthority }
                        projection.Sessions }

    let foldExecution
        (projection: AgentProjectionSet)
        (fact: ExecutionFactCases)
        : Result<AgentProjectionSet, FoldRejection> =
        match ExecutionFactFold.fold (sessionState projection) fact with
        | Ok changes -> Ok(List.fold applyChange projection changes)
        | Error rejection ->
            FoldRejection.reject (DelegationFoldRejection.fact rejection) (DelegationFoldRejection.message rejection)

    let foldDelegation
        (projection: AgentProjectionSet)
        (fact: DelegationFactCases)
        : Result<AgentProjectionSet, FoldRejection> =
        match DelegationFactFold.fold (sessionState projection) (handoffFrontier projection) fact with
        | Ok changes -> Ok(List.fold applyChange projection changes)
        | Error rejection ->
            FoldRejection.reject (DelegationFoldRejection.fact rejection) (DelegationFoldRejection.message rejection)
