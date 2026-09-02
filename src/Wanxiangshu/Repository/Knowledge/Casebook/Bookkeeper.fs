namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

/// CASE-006 Host Bookkeeper — freeze replayed observations, run one CaseRefresh
/// child session, stability-verify, then publish InspectorCaseRefreshed.
/// Missing session port or transaction Error keeps the old Case.
module CasebookBookkeeper =

    let private refreshPresentCase
        (store: IEventStore)
        (root: string)
        (sessionId: string)
        (case: Case)
        : Task<Result<bool, string>> =
        taskResult {
            let freeze = CasebookReplay.replayAll root case.Observations

            let! q', a' =
                BookkeeperRuntime.runTransaction
                    BookkeeperRequest.CaseRefresh
                    (SessionId.create sessionId)
                    case.Q
                    case.A
                    freeze
                    None

            let verify = CasebookReplay.replayAll root case.Observations

            match Observations.classifyReplay freeze verify with
            | ReplayResult.Stale ->
                return! Error "casebook synthesis unstable: worktree changed during bookkeeper transaction"
            | ReplayResult.Fresh ->
                do! CasebookWorkflow.refreshCase store sessionId q' a' freeze
                CasebookIndex.invalidate ()
                let! _ = CasebookIndex.refresh store 256 |> TaskResultCE.ofTask
                return true
        }

    let private refreshIfCasePresent
        (store: IEventStore)
        (root: string)
        (sessionId: string)
        : Task<Result<bool, string>> =
        taskResult {
            let! caseOpt = CasebookWorkflow.fetchCase store 256 sessionId

            match caseOpt with
            | None -> return false
            | Some case -> return! refreshPresentCase store root sessionId case
        }

    /// Returns Ok true when a Refreshed event was published; Ok false when
    /// Fresh / no-case (nothing to do). Error on store, transaction, or
    /// stability-verify failure — the old Case is left intact.
    let refreshStale (store: IEventStore) (root: string) (sessionId: string) : Task<Result<bool, string>> =
        taskResult {
            let! needs = CasebookWorkflow.needsRefresh store 256 sessionId root

            if not needs then
                return false
            else
                return! refreshIfCasePresent store root sessionId
        }
