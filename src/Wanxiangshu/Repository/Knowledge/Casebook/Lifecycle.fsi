namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

/// CASE-003/010 / KR-010: process-local Casebook session wiring.
module CasebookLifecycle =

    val collector: CasebookObservationCollector

    val setEnabled: workspaceRoot: string option -> unit

    val isEnabled: unit -> bool

    val notePrompt: delegateSessionId: string -> q: string -> unit

    val noteAnswer: delegateSessionId: string -> a: string -> unit

    val cleanupDraft: delegateSessionId: string -> unit

    val tryFinalizeDraft:
        workspaceRoot: string -> store: IEventStore -> delegateSessionId: string -> Task<CaseFinalizeSettlement>

    val finalizeEngineerCase:
        store: IEventStore ->
        identity: string ->
        sourceTrace: string ->
        q: string ->
        a: string ->
        relatedPaths: string list ->
        completionStateRef: string ->
            Task<CaseFinalizeSettlement>

    val touchAccess: workspaceRoot: string -> store: IEventStore -> sessionId: string -> Task<unit>
