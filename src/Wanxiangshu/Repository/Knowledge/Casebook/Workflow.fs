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
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
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
            match! CasebookStore.appendCaptured store case with
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

    /// CASE-006: publish a Bookkeeper revision as InspectorCaseRefreshed
    /// (linear parent). The caller (Bookkeeper orchestration) supplies the
    /// revised Q/A and the re-stabilized observations; failure keeps the old
    /// Case intact — maintenance failure is never a fetch failure.
    let refreshCase
        (store: IEventStore)
        (sessionId: string)
        (q: string)
        (a: string)
        (observations: Observation list)
        : Task<Result<unit, string>> =
        task {
            match! CasebookStore.appendRefreshed store sessionId q a observations with
            | Ok _ -> return Ok()
            | Error err -> return Error err
        }

    /// CASE-006: the full refresh decision — fetch the Case, replay against
    /// the current worktree, and report whether a Bookkeeper revision is
    /// needed (Stale) or the old answer still matches (Fresh / no-case).
    let needsRefresh
        (store: IEventStore)
        (capacity: int)
        (sessionId: string)
        (root: string)
        : Task<Result<bool, string>> =
        task {
            match! fetchCase store capacity sessionId with
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

        archiveInspectorResult store case

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
