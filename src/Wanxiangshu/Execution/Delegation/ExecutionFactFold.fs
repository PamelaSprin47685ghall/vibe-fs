namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable.ProjectionUpdate
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.ProjectionUpdate

module ExecutionFactFold =

    let private reject = FoldRejection.reject

    let private handleOutcome factName projection result =
        match result with
        | Ok updated -> Ok updated
        // Replaying a completion, abandon, or retirement is expected; durable
        // terminals make those idempotent.
        | Error AlreadyCompleted
        | Error AlreadyAbandoned
        | Error HandleIsRetired -> Ok projection
        | Error UnknownHandle -> reject factName "handle completion or retirement for a handle that was never linked"
        | Error NotCompleted -> reject factName "join retired a handle that had no completion (EXEC-004)"

    /// PERSIST-008: keep `HandleByChildSession` in step with a handle change.
    ///
    /// Runs after the per-session fold succeeded, so the index always mirrors the
    /// authoritative `Handles` map. `handleOutcome` absorbs duplicate replays, and
    /// a replay re-syncs the index to the same record — idempotent by
    /// construction.
    let private syncHandleIndex (parentId: SessionId) (handle: HandleId) (projection: AgentProjectionSet) =
        match AgentProjection.tryFind parentId projection with
        | Some session ->
            match session.Handles with
            | Some handles ->
                match HandleProjection.tryFind handle handles with
                | Some record ->
                    { projection with
                        HandleByChildSession = Map.add record.ChildSessionId record projection.HandleByChildSession }
                | None -> projection
            | None -> projection
        | None -> projection

    let fold (projection: AgentProjectionSet) (fact: ExecutionFactCases) : Result<AgentProjectionSet, FoldRejection> =
        // ── execution handles ───────────────────────────────────────────────
        match fact with
        | ExecutionFactCases.HandleLinked payload ->
            let byname =
                if System.String.IsNullOrWhiteSpace payload.Byname then
                    payload.TargetAgent
                else
                    payload.Byname

            AgentProjection.tryUpdate
                payload.ParentSessionId
                (fun session ->
                    HandleProjection.linkNamed
                        payload.Handle
                        payload.ChildSessionId
                        payload.TargetAgent
                        byname
                        payload.CanonicalRole
                        payload.Ownership
                        (Option.defaultValue HandleProjection.empty session.Handles)
                    |> Result.map (fun updated -> { session with Handles = Some updated }))
                projection
            |> handleOutcome "HandleLinked" projection
            |> Result.map (syncHandleIndex payload.ParentSessionId payload.Handle)

        | ExecutionFactCases.HandleCompleted payload ->
            AgentProjection.tryUpdate
                payload.ParentSessionId
                (fun session ->
                    HandleProjection.complete
                        payload.Handle
                        { Kind = payload.Kind
                          CompletionRef = payload.CompletionRef
                          CompletionDigest = payload.CompletionDigest }
                        (Option.defaultValue HandleProjection.empty session.Handles)
                    |> Result.map (fun updated -> { session with Handles = Some updated }))
                projection
            |> handleOutcome "HandleCompleted" projection
            |> Result.map (syncHandleIndex payload.ParentSessionId payload.Handle)

        | ExecutionFactCases.HandleRetired payload ->
            AgentProjection.tryUpdate
                payload.ParentSessionId
                (fun session ->
                    HandleProjection.retire payload.Handle (Option.defaultValue HandleProjection.empty session.Handles)
                    |> Result.map (fun updated -> { session with Handles = Some updated }))
                projection
            |> handleOutcome "HandleRetired" projection
            |> Result.map (syncHandleIndex payload.ParentSessionId payload.Handle)

        | ExecutionFactCases.HandleAbandoned payload ->
            AgentProjection.tryUpdate
                payload.ParentSessionId
                (fun session ->
                    HandleProjection.abandon
                        payload.Handle
                        payload.Reason
                        (Option.defaultValue HandleProjection.empty session.Handles)
                    |> Result.map (fun updated -> { session with Handles = Some updated }))
                projection
            |> handleOutcome "HandleAbandoned" projection
            |> Result.map (syncHandleIndex payload.ParentSessionId payload.Handle)

        // Clean-break: false abort cell → Active only when ref/digest match.

        | ExecutionFactCases.HandleFalseCompletionRejected payload ->
            AgentProjection.tryUpdate
                payload.ParentSessionId
                (fun session ->
                    HandleProjection.rejectFalseCompletion
                        payload.Handle
                        payload.ExpectedCompletionRef
                        payload.ExpectedCompletionDigest
                        (Option.defaultValue HandleProjection.empty session.Handles)
                    |> Result.map (fun updated -> { session with Handles = Some updated }))
                projection
            |> handleOutcome "HandleFalseCompletionRejected" projection
            |> Result.map (syncHandleIndex payload.ParentSessionId payload.Handle)

        // Clean-break: retired false terminal report. Projection keeps original
        // Retired tombstone; replacement is linked by a separate HandleLinked.

        | ExecutionFactCases.HandleFalseTerminalReported _ -> Ok projection

        // Clean-break: parent correction notice. No handle lifecycle change.

        | ExecutionFactCases.ParentJoinCorrectionRequested _ -> Ok projection

        // HostTurnObserved is a durable observation inbox fact. CompletionReactor
        // (later batch) consumes it; LinkageProjection has no fold effect yet.

        | ExecutionFactCases.HostTurnObserved _ -> Ok projection
