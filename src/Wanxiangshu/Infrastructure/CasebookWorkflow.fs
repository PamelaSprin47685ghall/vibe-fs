namespace Wanxiangshu.Infrastructure

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
    let archiveInspectorResult (store: IEventStore) (raw: IGitRawStore) (case: Case) : Result<unit, string> =
        match CasebookStore.loadEnvelopes raw (store.OpenSnapshot()) with
        | Error err -> Error err
        | Ok envelopes ->
            let parents = CasebookStore.headOf envelopes |> Option.toList

            match CasebookStore.appendCaptured store parents case with
            | Ok _ -> Ok()
            | Error err -> Error err

    /// Fetch one Case by session id (CASE-004).
    let fetchCase
        (store: IEventStore)
        (raw: IGitRawStore)
        (capacity: int)
        (sessionId: string)
        : Result<Case option, string> =
        match CasebookStore.loadEvents raw (store.OpenSnapshot()) with
        | Error err -> Error err
        | Ok events ->
            let cases = CasebookStore.project capacity events
            Ok(Map.tryFind sessionId cases)

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
        : Result<unit, string> =
        match CasebookStore.loadEnvelopes raw (store.OpenSnapshot()) with
        | Error err -> Error err
        | Ok envelopes ->
            let parents = CasebookStore.headOf envelopes |> Option.toList

            match CasebookStore.appendRefreshed store parents sessionId q a observations with
            | Ok _ -> Ok()
            | Error err -> Error err

    /// CASE-006: the full refresh decision — fetch the Case, replay against
    /// the current worktree, and report whether a Bookkeeper revision is
    /// needed (Stale) or the old answer still matches (Fresh / no-case).
    let needsRefresh
        (store: IEventStore)
        (raw: IGitRawStore)
        (capacity: int)
        (sessionId: string)
        (root: string)
        : Result<bool, string> =
        match fetchCase store raw capacity sessionId with
        | Error err -> Error err
        | Ok None -> Ok false
        | Ok(Some case) ->
            let replayed = CasebookReplay.replayAll root case.Observations

            match checkFreshness case replayed with
            | ReplayResult.Fresh -> Ok false
            | ReplayResult.Stale -> Ok true

    /// CASE-010: exactly-one CaseFinalize — a reusable Inspector scope archives
    /// at most once (ReuseScope close → freeze draft → one finalize). A second
    /// finalize for the same session id is refused; unexpected SessionDeleted
    /// must not reconstruct a pending finalize (the caller just cleans up).
    let finalizeCase (store: IEventStore) (raw: IGitRawStore) (case: Case) : Result<unit, string> =
        match fetchCase store raw 0 case.SessionId with
        | Error err -> Error err
        | Ok(Some _) -> Error(sprintf "case already finalized for scope %s" case.SessionId)
        | Ok None -> archiveInspectorResult store raw case
