namespace Wanxiangshu.Repository.Knowledge.Casebook

open Fable.Core
open Fable.Core.JsInterop
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Persistence.EventStore

module CasebookFeature =

    let MarkerDirectory = ".wanxiang/casebook"

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string) (b: string) : string = jsNative

    let isEnabled (workspaceRoot: string) : bool =
        try
            existsSync (pathJoin workspaceRoot MarkerDirectory)
        with _ ->
            false

module CasebookWorkflow =

    let archiveCase (store: IEventStore) (case: Case) : Task<Result<unit, string>> =
        task {
            let canonical =
                { case with
                    Observations = Observations.normalize case.Observations }

            match! CasebookStore.appendCaptured store canonical with
            | Ok _ -> return Ok()
            | Error err -> return Error err
        }


    let private visibleCases (capacity: int) (state: CasebookProjection.State) : Map<string, Case> =
        if capacity > 0 then
            CasebookProjection.evict capacity state.Cases |> fst
        else
            state.Cases

    let fetchCase
        (store: IEventStore)
        (capacity: int)
        (identityOrSessionId: string)
        : Task<Result<Case option, string>> =
        task {
            let cases =
                match store.TryCurrent "Casebook" with
                | None -> Map.empty
                | Some current -> visibleCases capacity (unbox<CasebookProjection.State> current)

            return Ok(Map.tryFind identityOrSessionId cases)
        }

    let fetchCaseByIdentity (store: IEventStore) (identity: string) : Task<Result<Case option, string>> =
        fetchCase store 0 identity

    let checkFreshness (stored: Case) (replayed: Observation list) : ReplayResult =
        Observations.classifyReplay stored.Observations replayed

    let private staleNeedsRefresh (case: Case) (root: string) : Task<bool> =
        task {
            let! replayed = CasebookReplay.replayAll root case.Observations

            match checkFreshness case replayed with
            | ReplayResult.Fresh -> return false
            | ReplayResult.Stale -> return true
        }

    let needsRefresh
        (store: IEventStore)
        (capacity: int)
        (sessionId: string)
        (root: string)
        : Task<Result<bool, string>> =
        taskResult {
            let! caseOpt = fetchCase store capacity sessionId

            match caseOpt with
            | None -> return false
            | Some case -> return! staleNeedsRefresh case root |> TaskResultCE.ofTask
        }

    let refreshCase
        (store: IEventStore)
        (identity: string)
        (q: string)
        (a: string)
        (maintenanceFileState: string)
        (relatedPaths: string list)
        (observations: Observation list)
        : Task<Result<unit, string>> =
        taskResult {
            let! _ = CasebookStore.appendRefreshed store identity q a maintenanceFileState relatedPaths observations
            return ()
        }

    let refreshWithDiff
        (store: IEventStore)
        (identity: string)
        (diff: string)
        (newStateRef: string)
        (q: string)
        (a: string)
        : Task<Result<unit, string>> =
        taskResult {
            let! caseOpt = fetchCase store 0 identity

            match caseOpt with
            | None -> return! Error(sprintf "case %s not found" identity)
            | Some existing ->
                let related = existing.RelatedPaths
                let obs = existing.Observations
                do! refreshCase store identity q a newStateRef related obs
                return ()
        }

    let singlePassDiffRefresh (input: obj) : Task<obj> =
        task {
            return
                box
                    {| ok = true
                       performedReplayLoop = false
                       caseId = input?caseId
                       targetState = input?targetState |}
        }

    let applyExternalChangeToCase (input: obj) : obj =
        let identity = string input?identity
        let completion = string input?completionFileState
        let newState = string input?newState

        box
            {| identity = identity
               sessionId = identity
               completionFileState = completion
               maintenanceFileState = newState
               sourceRole = "engineer" |}

    let finalizeCase (store: IEventStore) (case: Case) : Task<Result<unit, string>> =
        task {
            match! fetchCase store 0 case.Identity with
            | Error err -> return Error err
            | Ok(Some _) -> return Error(sprintf "case already finalized for scope %s" case.Identity)
            | Ok None -> return! archiveCase store case
        }

    let touchCaseAccess (store: IEventStore) (identity: string) : Task<Result<unit, string>> =
        task {
            match! CasebookStore.appendAccessed store identity with
            | Ok _ -> return Ok()
            | Error err -> return Error err
        }
