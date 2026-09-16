namespace Wanxiangshu.Repository.Knowledge.Casebook

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

/// CASE-006 / KR-006 / KR-015: Host Bookkeeper — single-pass diff refresh.
/// Missing session port or transaction Error keeps the old Case.
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

    let private refreshPresentCase
        (store: IEventStore)
        (root: string)
        (sessionId: string)
        (case: Case)
        : Task<Result<bool, string>> =
        taskResult {
            let paths = extractPaths case
            let! targetState = CasebookCapture.freezeCompletionState root paths |> TaskResultCE.ofTask
            let! diffObj = CasebookCapture.computeMaintenanceDiff root targetState |> TaskResultCE.ofTask
            let hasDiff = unbox<bool> (diffObj?hasDiff)
            let diffSummary = unbox<string> (diffObj?diffSummary)

            if not hasDiff then
                return false
            else
                let! (q', a') =
                    BookkeeperRuntime.runTransaction
                        BookkeeperRequest.CaseRefresh
                        (SessionId.create sessionId)
                        case.Q
                        case.A
                        case.Observations
                        (Some diffSummary)

                let newStateRef =
                    if String.IsNullOrWhiteSpace diffSummary then
                        case.MaintenanceFileState
                    else
                        sprintf "state-%s" (CasebookCapture.contentHash diffSummary)

                do! CasebookWorkflow.refreshCase store case.Identity q' a' newStateRef paths case.Observations
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

    let refreshStale (store: IEventStore) (root: string) (sessionId: string) : Task<Result<bool, string>> =
        taskResult {
            return! refreshIfCasePresent store root sessionId
        }
