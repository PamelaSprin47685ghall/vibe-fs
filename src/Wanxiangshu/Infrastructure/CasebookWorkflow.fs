namespace Wanxiangshu.Infrastructure

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Persist

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

    /// Archive one Inspector result as InspectorCaseCaptured (linear parent).
    /// Q is the verbatim initial prompt, A the verbatim ToolResult body.
    let archiveInspectorResult (store: IEventStore) (raw: IGitRawStore) (case: Case) : Task<Result<unit, string>> =
        task {
            let! snapshot = store.OpenSnapshot()

            match! CasebookStore.loadEnvelopes raw snapshot with
            | Error err -> return Error err
            | Ok envelopes ->
                let parents = CasebookStore.headOf envelopes |> Option.toList

                match! CasebookStore.appendCaptured store parents case with
                | Ok _ -> return Ok()
                | Error err -> return Error err
        }

    /// Fetch one Case by session id (CASE-004).
    let fetchCase
        (store: IEventStore)
        (raw: IGitRawStore)
        (capacity: int)
        (sessionId: string)
        : Task<Result<Case option, string>> =
        task {
            let! snapshot = store.OpenSnapshot()

            match! CasebookStore.loadEvents raw snapshot with
            | Error err -> return Error err
            | Ok events ->
                let cases = CasebookStore.project capacity events
                return Ok(Map.tryFind sessionId cases)
        }

    /// CASE-004/005: freshness is a hint, never a proof — exact normalized
    /// equality of stored vs replayed observations.
    let checkFreshness (stored: Case) (replayed: Observation list) : ReplayResult =
        Observations.classifyReplay stored.Observations replayed

    /// CASE-006: publish a Bookkeeper revision as InspectorCaseRefreshed
    /// (linear parent). The caller (Bookkeeper orchestration) supplies the
    /// revised Q/A and the re-stabilized observations; failure keeps the old
    /// Case intact — maintenance failure is never a fetch failure.
    let refreshCase
        (store: IEventStore)
        (raw: IGitRawStore)
        (sessionId: string)
        (q: string)
        (a: string)
        (observations: Observation list)
        : Task<Result<unit, string>> =
        task {
            let! snapshot = store.OpenSnapshot()

            match! CasebookStore.loadEnvelopes raw snapshot with
            | Error err -> return Error err
            | Ok envelopes ->
                let parents = CasebookStore.headOf envelopes |> Option.toList

                match! CasebookStore.appendRefreshed store parents sessionId q a observations with
                | Ok _ -> return Ok()
                | Error err -> return Error err
        }

    /// CASE-006: the full refresh decision — fetch the Case, replay against
    /// the current worktree, and report whether a Bookkeeper revision is
    /// needed (Stale) or the old answer still matches (Fresh / no-case).
    let needsRefresh
        (store: IEventStore)
        (raw: IGitRawStore)
        (capacity: int)
        (sessionId: string)
        (root: string)
        : Task<Result<bool, string>> =
        task {
            match! fetchCase store raw capacity sessionId with
            | Error err -> return Error err
            | Ok None -> return Ok false
            | Ok(Some case) ->
                let replayed = CasebookReplay.replayAll root case.Observations

                match checkFreshness case replayed with
                | ReplayResult.Fresh -> return Ok false
                | ReplayResult.Stale -> return Ok true
        }

    /// ponytail: drain+archive wiring — single helper for Inspector terminal; full SessionDeleted wiring if throughput matters
    let drainCollectorAndArchive
        (collector: ObservationCollector)
        (store: IEventStore)
        (raw: IGitRawStore)
        (sessionId: string)
        (q: string)
        (a: string)
        : Task<Result<unit, string>> =
        let observations = collector.Drain sessionId

        let case: Case =
            { SessionId = sessionId
              Q = q
              A = a
              Observations = observations
              LastAccessOrder = 0L }

        archiveInspectorResult store raw case

    /// CASE-010: exactly-one CaseFinalize — a reusable Inspector scope archives
    /// at most once (ReuseScope close → freeze draft → one finalize). A second
    /// finalize for the same session id is refused; unexpected SessionDeleted
    /// must not reconstruct a pending finalize (the caller just cleans up).
    let finalizeCase (store: IEventStore) (raw: IGitRawStore) (case: Case) : Task<Result<unit, string>> =
        task {
            match! fetchCase store raw 0 case.SessionId with
            | Error err -> return Error err
            | Ok(Some _) -> return Error(sprintf "case already finalized for scope %s" case.SessionId)
            | Ok None -> return! archiveInspectorResult store raw case
        }

    /// CASE-007: append InspectorCaseAccessed with the current stream head as parent.
    let touchCaseAccess (store: IEventStore) (raw: IGitRawStore) (sessionId: string) : Task<Result<unit, string>> =
        task {
            let! snapshot = store.OpenSnapshot()

            match! CasebookStore.loadEnvelopes raw snapshot with
            | Error err -> return Error err
            | Ok envelopes ->
                let parents = CasebookStore.headOf envelopes |> Option.toList

                match! CasebookStore.appendAccessed store parents sessionId with
                | Ok _ -> return Ok()
                | Error err -> return Error err
        }
