namespace Wanxiangshu.Repository.Knowledge.Casebook

open Fable.Core
open Fable.Core.JsInterop
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Persistence.EventStore

/// CASE-009: feature gating — the product surface lives only when the marker
/// directory exists. Disabling closes schema, execution, capture, archive and
/// Bookkeeper; it never touches the unified store (Persist owns that).
module CasebookFeature =

    /// The opt-in marker (§3.1): directory existence only; `.keep` contents are
    /// never interpreted.
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

/// CASE-003/004/005: the Casebook workflow — archive, fetch, freshness check.
/// Archive failure is NOT an Inspector call failure: every function returns a
/// Result and the caller decides how to surface it.
module CasebookWorkflow =

    /// Archive one Inspector result. Structural parent selection belongs to the
    /// canonical Integrator/store, not to a feature-owned history scan.
    let archiveInspectorResult (store: IEventStore) (case: Case) : Task<Result<unit, string>> =
        task {
            let canonical =
                { case with
                    Observations = Observations.normalize case.Observations }

            match! CasebookStore.appendCaptured store canonical with
            | Ok _ -> return Ok()
            | Error err -> return Error err
        }

    /// Fetch one Case by session id (CASE-004).
    let fetchCase (store: IEventStore) (capacity: int) (sessionId: string) : Task<Result<Case option, string>> =
        task {
            let cases =
                match store.TryCurrent "Casebook" with
                | None -> Map.empty
                | Some current ->
                    let state = unbox<CasebookProjection.State> current
                    CasebookProjection.evict capacity state.Cases |> fst

            return Ok(Map.tryFind sessionId cases)
        }

    /// CASE-004/005: freshness is a hint, never a proof — exact normalized
    /// equality of stored vs replayed observations.
    let checkFreshness (stored: Case) (replayed: Observation list) : ReplayResult =
        Observations.classifyReplay stored.Observations replayed

    let private staleNeedsRefresh (case: Case) (root: string) =
        match checkFreshness case (CasebookReplay.replayAll root case.Observations) with
        | ReplayResult.Fresh -> false
        | ReplayResult.Stale -> true

    /// CASE-006: the full refresh decision — fetch the Case, replay against
    /// the current worktree, and report whether a Bookkeeper revision is
    /// needed (Stale) or the old answer still matches (Fresh / no-case).
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
            | Some case -> return staleNeedsRefresh case root
        }

    let refreshCase
        (store: IEventStore)
        (sessionId: string)
        (q: string)
        (a: string)
        (observations: Observation list)
        : Task<Result<unit, string>> =
        taskResult {
            let! _ = CasebookStore.appendRefreshed store sessionId q a observations
            return ()
        }

    /// CASE-010: exactly-one CaseFinalize — a reusable Inspector scope archives
    /// at most once (ReuseScope close → freeze draft → one finalize). A second
    /// finalize for the same session id is refused; unexpected SessionDeleted
    /// must not reconstruct a pending finalize (the caller just cleans up).
    let finalizeCase (store: IEventStore) (case: Case) : Task<Result<unit, string>> =
        task {
            match! fetchCase store 0 case.SessionId with
            | Error err -> return Error err
            | Ok(Some _) -> return Error(sprintf "case already finalized for scope %s" case.SessionId)
            | Ok None -> return! archiveInspectorResult store case
        }

    /// CASE-007: append InspectorCaseAccessed; structural parent comes from Current.
    let touchCaseAccess (store: IEventStore) (sessionId: string) : Task<Result<unit, string>> =
        task {
            match! CasebookStore.appendAccessed store sessionId with
            | Ok _ -> return Ok()
            | Error err -> return Error err
        }
