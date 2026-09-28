namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session.ChatExecution
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

    /// A child work run (AgentOwnerRoot) that ended without completing — the
    /// process died, the turn was cancelled, the dispatch was rejected — leaves
    /// no live execution behind. crash-reconciliation-017/020 forbid restarting
    /// interrupted work automatically: restarting the road's child stays the
    /// manager's explicit decision. But the durable bookkeeping must be reset
    /// first, or the road could never start that child again:
    ///
    ///  - the child's logical run is closed, so the next handoff roots a fresh
    ///    AgentOwnerRoot — horizon and join see a child that is just born, while
    ///    its transcript stays naturally preserved;
    ///  - the parent's handle for that child is settled as a `Cancelled`
    ///    completion, so a pending join receives an explicit outcome instead of
    ///    waiting forever on a run that will never finish. That is a completion
    ///    report, not an Abandon declaration (managed-session-lifecycle-018).
    let private closeChildAuthority (childSessionId: SessionId) (projection: AgentProjectionSet) =
        AgentProjection.tryUpdate
            childSessionId
            (fun session ->
                session.PromptAuthority
                |> Option.bind (fun authority ->
                    authority.ActiveLogicalRun
                    |> Option.bind (fun active ->
                        if active.AuthorityKind <> PromptAuthority.RootAuthorityKind.AgentOwnerRoot then
                            None
                        else
                            PromptAuthorityRun.closeCompletedAgentOwnerChildWork
                                active.LogicalRunId
                                active.AuthorityRootUserMessageId
                                authority
                            |> Result.toOption))
                |> Option.map (fun closed ->
                    Ok
                        { session with
                            PromptAuthority = Some closed })
                |> Option.defaultValue (Ok session))
            projection
        |> Result.defaultValue projection

    let private settleParentHandle (childSessionId: SessionId) (projection: AgentProjectionSet) =
        let settled =
            projection.HandleByChildSession
            |> Map.tryFind childSessionId
            |> Option.bind (fun record ->
                projection.Sessions
                |> Map.toList
                |> List.tryPick (fun (parentSessionId, session) ->
                    match session.Handles with
                    | Some handles when HandleProjection.tryFind record.Handle handles |> Option.isSome ->
                        HandleProjection.complete
                            record.Handle
                            { Kind = HandleCompletionKind.Cancelled
                              CompletionRef = None
                              CompletionDigest = None }
                            handles
                        |> Result.toOption
                        |> Option.map (fun completed -> parentSessionId, completed)
                    | _ -> None))

        match settled with
        | None -> projection
        | Some(parentSessionId, handles) ->
            AgentProjection.tryUpdate
                parentSessionId
                (fun session -> Ok { session with Handles = Some handles })
                projection
            |> Result.defaultValue projection

    let settleUncompletedChildRun (projection: AgentProjectionSet) (fact: ChatExecutionFactCases) : AgentProjectionSet =
        match fact with
        | ChatExecutionFactCases.Terminal payload when payload.Disposition <> ChatExecutionTerminalDisposition.Completed ->
            let childSessionId = payload.Key.SessionId

            projection
            |> closeChildAuthority childSessionId
            |> settleParentHandle childSessionId

        | _ -> projection
