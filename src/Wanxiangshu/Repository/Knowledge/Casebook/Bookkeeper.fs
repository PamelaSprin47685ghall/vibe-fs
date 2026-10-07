namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

/// CASE-006 / KR-006 / KR-015: Host Bookkeeper — single-pass diff refresh.
/// Missing session port or transaction Error refuses refresh; an ambiguous
/// append retains its evidence without claiming that Current stayed unchanged.
module CasebookBookkeeper =

    let private extractPaths (case: Case) : string list =
        if not (List.isEmpty case.RelatedPaths) then
            case.RelatedPaths
        else
            case.Observations
            |> List.choose (function
                | Observation.FileRead(path, _) -> Some path
                | _ -> None)
            |> List.distinct
            |> List.sort

    /// Run the Bookkeeper transaction with the real diff and publish the result.
    let private applyRefresh
        (store: IEventStore)
        (sessionId: string)
        (paths: string list)
        (targetState: string)
        (diffSummary: string)
        (case: Case)
        : Task<Result<bool, CasebookMutationError>> =
        taskResult {
            let! (q', a') =
                BookkeeperRuntime.runTransaction
                    BookkeeperRequest.CaseRefresh
                    (SessionId.create sessionId)
                    case.Q
                    case.A
                    case.Observations
                    (Some diffSummary)
                |> TaskResult.mapError CasebookMutationError.PreparationRejected

            do! CasebookWorkflow.refreshCase store case.Identity q' a' targetState paths case.Observations

            CasebookIndex.invalidate ()
            let! _ = CasebookIndex.refresh store 256 |> TaskResultCE.ofTask
            return true
        }

    let private refreshPresentCase
        (store: IEventStore)
        (root: string)
        (sessionId: string)
        (case: Case)
        : Task<Result<bool, CasebookMutationError>> =
        taskResult {
            let paths = extractPaths case

            let! captured =
                CasebookCapture.computeMaintenanceDiff store root paths case.MaintenanceFileState
                |> TaskResult.mapError CasebookMutationError.PreparationRejected

            if captured.DiffSummary <> "" then
                return! applyRefresh store sessionId paths captured.TargetState captured.DiffSummary case
            else
                return false
        }

    let private refreshIfCasePresent
        (store: IEventStore)
        (root: string)
        (sessionId: string)
        : Task<Result<bool, CasebookMutationError>> =
        taskResult {
            let! caseOpt =
                CasebookWorkflow.fetchCase store 256 sessionId
                |> TaskResult.mapError CasebookMutationError.PreparationRejected

            match caseOpt with
            | None -> return false
            | Some case -> return! refreshPresentCase store root sessionId case
        }

    let refreshStale
        (store: IEventStore)
        (root: string)
        (sessionId: string)
        : Task<Result<bool, CasebookMutationError>> =
        taskResult { return! refreshIfCasePresent store root sessionId }
