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

    let private presentHashIn (targetState: obj) (p: string) : string option =
        let entry = emitJsExpr (targetState, p) "$0.get($1)"

        if isNull entry || unbox<string> (entry?kind) <> "Present" then
            None
        else
            Some(unbox<string> (entry?contentHash))

    /// A read observation keeps its path but adopts the maintained content hash.
    let private rehashReadObservation (targetState: obj) (obs: Observation) : Observation option =
        match obs with
        | Observation.FileRead(p, _) -> presentHashIn targetState p |> Option.map (fun h -> Observation.FileRead(p, h))
        | other -> Some other

    let private storedStateRef (case: Case) (diffSummary: string) : string =
        if String.IsNullOrWhiteSpace diffSummary then
            case.MaintenanceFileState
        else
            sprintf "state-%s" (CasebookCapture.contentHash diffSummary)

    /// Run the Bookkeeper transaction with the real diff and publish the result.
    let private applyRefresh
        (store: IEventStore)
        (sessionId: string)
        (paths: string list)
        (targetState: string)
        (diffSummary: string)
        (case: Case)
        : Task<Result<bool, string>> =
        taskResult {
            let! (q', a') =
                BookkeeperRuntime.runTransaction
                    BookkeeperRequest.CaseRefresh
                    (SessionId.create sessionId)
                    case.Q
                    case.A
                    case.Observations
                    (Some diffSummary)

            let updatedObservations = case.Observations

            let newMaintenanceState =
                if not (String.IsNullOrWhiteSpace targetState) then
                    targetState
                else
                    storedStateRef case diffSummary

            do! CasebookWorkflow.refreshCase store case.Identity q' a' newMaintenanceState paths updatedObservations

            CasebookIndex.invalidate ()
            let! _ = CasebookIndex.refresh store 256 |> TaskResultCE.ofTask
            return true
        }

    let private refreshPresentCase
        (store: IEventStore)
        (root: string)
        (sessionId: string)
        (case: Case)
        : Task<Result<bool, string>> =
        taskResult {
            let paths = extractPaths case

            let baseline =
                if
                    not (String.IsNullOrWhiteSpace case.MaintenanceFileState)
                    && case.MaintenanceFileState <> "state-initial"
                then
                    box case.MaintenanceFileState
                elif
                    not (String.IsNullOrWhiteSpace case.CompletionFileState)
                    && case.CompletionFileState <> "state-initial"
                then
                    box case.CompletionFileState
                else
                    CasebookCapture.baselineFromObservations case.Observations case.RelatedPaths

            let! diffObj = CasebookCapture.computeMaintenanceDiff root baseline |> TaskResultCE.ofTask
            let! targetState = CasebookCapture.freezeCompletionState store root paths
            let hasDiff = unbox<bool> (diffObj?hasDiff)
            let diffSummary = unbox<string> (diffObj?diffSummary)

            if hasDiff then
                return! applyRefresh store sessionId paths targetState diffSummary case
            else
                return false
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
        taskResult { return! refreshIfCasePresent store root sessionId }
