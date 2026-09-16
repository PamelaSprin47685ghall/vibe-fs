namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

/// CASE-009: feature gating — the product surface lives only when the marker
/// directory exists.
module CasebookFeature =

    val MarkerDirectory: string
    val isEnabled: workspaceRoot: string -> bool

/// CASE-003/004/005 / KR-004/005/015: Casebook workflow.
module CasebookWorkflow =

    val archiveCase: store: IEventStore -> case: Case -> Task<Result<unit, string>>

    val fetchCase:
        store: IEventStore -> capacity: int -> identityOrSessionId: string -> Task<Result<Case option, string>>

    val fetchCaseByIdentity: store: IEventStore -> identity: string -> Task<Result<Case option, string>>
    val checkFreshness: stored: Case -> replayed: Observation list -> ReplayResult

    val needsRefresh:
        store: IEventStore -> capacity: int -> sessionId: string -> root: string -> Task<Result<bool, string>>

    val refreshCase:
        store: IEventStore ->
        identity: string ->
        q: string ->
        a: string ->
        maintenanceFileState: string ->
        relatedPaths: string list ->
        observations: Observation list ->
            Task<Result<unit, string>>

    val refreshWithDiff:
        store: IEventStore ->
        identity: string ->
        diff: string ->
        newStateRef: string ->
        q: string ->
        a: string ->
            Task<Result<unit, string>>

    val singlePassDiffRefresh: input: obj -> Task<obj>
    val applyExternalChangeToCase: input: obj -> obj
    val finalizeCase: store: IEventStore -> case: Case -> Task<Result<unit, string>>
    val touchCaseAccess: store: IEventStore -> identity: string -> Task<Result<unit, string>>
