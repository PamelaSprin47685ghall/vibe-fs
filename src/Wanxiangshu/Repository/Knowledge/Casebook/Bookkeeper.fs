namespace Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System.Threading.Tasks
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
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Persistence.EventStore

/// CASE-006 Host Bookkeeper — freeze replayed observations, run one CaseRefresh
/// child session, stability-verify, then publish InspectorCaseRefreshed.
/// Missing session port or transaction Error keeps the old Case.
module CasebookBookkeeper =

    /// Returns Ok true when a Refreshed event was published; Ok false when
    /// Fresh / no-case (nothing to do). Error on store, transaction, or
    /// stability-verify failure — the old Case is left intact.
    let refreshStale
        (store: IEventStore)
        (root: string)
        (sessionId: string)
        : Task<Result<bool, string>> =
        task {
            match! CasebookWorkflow.needsRefresh store 256 sessionId root with
            | Error err -> return Error err
            | Ok false -> return Ok false
            | Ok true ->
                match! CasebookWorkflow.fetchCase store 256 sessionId with
                | Error err -> return Error err
                | Ok None -> return Ok false
                | Ok(Some case) ->
                    let freeze = CasebookReplay.replayAll root case.Observations

                    match!
                        BookkeeperRuntime.runTransaction
                            BookkeeperRequest.CaseRefresh
                            sessionId
                            case.Q
                            case.A
                            freeze
                            None
                    with
                    | Error err -> return Error err
                    | Ok(q', a') ->
                        let verify = CasebookReplay.replayAll root case.Observations

                        match Observations.classifyReplay freeze verify with
                        | ReplayResult.Stale ->
                            return Error "casebook synthesis unstable: worktree changed during bookkeeper transaction"
                        | ReplayResult.Fresh ->
                            match! CasebookWorkflow.refreshCase store sessionId q' a' freeze with
                            | Ok() ->
                                CasebookIndex.invalidate ()
                                let! _ = CasebookIndex.refresh store 256
                                return Ok true
                            | Error err -> return Error err
        }
