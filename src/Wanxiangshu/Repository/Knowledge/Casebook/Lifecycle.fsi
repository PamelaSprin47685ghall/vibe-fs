namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

/// CASE-003/010 / KR-010: process-local Casebook session wiring.
module CasebookLifecycle =

    val collector: CasebookObservationCollector

    val setEnabled: workspaceRoot: string option -> unit

    val isEnabled: unit -> bool

    val notePrompt: inspectorSessionId: string -> q: string -> unit

    val noteAnswer: inspectorSessionId: string -> a: string -> unit

    val cleanupInspector: inspectorSessionId: string -> unit

    val tryFinalizeInspector:
        workspaceRoot: string -> store: IEventStore -> inspectorSessionId: string -> Task<InspectorFinalizeSettlement>

    val finalizeEngineerCase:
        store: IEventStore ->
        identity: string ->
        sourceTrace: string ->
        q: string ->
        a: string ->
        relatedPaths: string list ->
        completionStateRef: string ->
            Task<InspectorFinalizeSettlement>

    val touchAccess: workspaceRoot: string -> store: IEventStore -> sessionId: string -> Task<unit>
