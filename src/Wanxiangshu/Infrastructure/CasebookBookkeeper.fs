namespace Wanxiangshu.Infrastructure

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Persist

/// CASE-006 Host Bookkeeper — freeze replayed observations, run one CaseRefresh
/// child session, stability-verify, then publish InspectorCaseRefreshed.
/// Missing session port or transaction Error keeps the old Case.
module CasebookBookkeeper =

    /// Returns Ok true when a Refreshed event was published; Ok false when
    /// Fresh / no-case (nothing to do). Error on store, transaction, or
    /// stability-verify failure — the old Case is left intact.
    let refreshStale
        (store: IEventStore)
        (raw: IGitRawStore)
        (root: string)
        (sessionId: string)
        : Task<Result<bool, string>> =
        task {
            match! CasebookWorkflow.needsRefresh store raw 256 sessionId root with
            | Error err -> return Error err
            | Ok false -> return Ok false
            | Ok true ->
                match! CasebookWorkflow.fetchCase store raw 256 sessionId with
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
                            match! CasebookWorkflow.refreshCase store raw sessionId q' a' freeze with
                            | Ok() ->
                                CasebookIndex.invalidate ()
                                let! _ = CasebookIndex.refresh store raw 256
                                return Ok true
                            | Error err -> return Error err
        }
