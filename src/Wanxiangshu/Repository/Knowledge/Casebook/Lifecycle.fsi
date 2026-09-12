namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

/// CASE-003/010: process-local Casebook session wiring — draft Q/A turns,
/// observation drain, graceful finalize vs unexpected cleanup. Publication
/// goes only through WorkspaceEventStore (unified store); never AgentJournal.
module CasebookLifecycle =

    /// Process-local singleton the plugin feeds; lifecycle drains it.
    val collector: CasebookObservationCollector

    /// Marker-gated enablement for the shared collector path. `None` or a root
    /// without `.wanxiang/casebook` disables; does not touch the store.
    val setEnabled: workspaceRoot: string option -> unit

    val isEnabled: unit -> bool

    /// Invoke: record Q for the inspector session id (appends a turn).
    val notePrompt: inspectorSessionId: string -> q: string -> unit

    /// Return: record A for the inspector session id (fills the current turn).
    val noteAnswer: inspectorSessionId: string -> a: string -> unit

    /// Unexpected delete / cancel: clear draft + drop collector buffer; NEVER append events.
    val cleanupInspector: inspectorSessionId: string -> unit

    /// Graceful owner scope close: if draft has Q+A, drain observations, run
    /// exactly one CaseFinalize child session with the full turn transcript,
    /// then finalizeCase once. Unexpected cleanup never runs Bookkeeper.
    /// exactly one CaseFinalize child session with the full turn transcript,
    /// then finalizeCase once. Store is explicit to keep Knowledge domain out of
    /// application composition.
    val tryFinalizeInspector:
        workspaceRoot: string -> store: IEventStore -> inspectorSessionId: string -> Task<Result<unit, string>>

    /// Fresh fetch side-effect: append InspectorCaseAccessed (ignore errors).
    val touchAccess: workspaceRoot: string -> store: IEventStore -> sessionId: string -> Task<unit>
