namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Repository.Programming.Js

/// Parse the sandbox JSON string into LlmFacing reference data and enforce JS-010.
module JsToolsData =
    val parse: json: string -> Result<LlmFacing.Data.Value, JsFailure>
    val parseParkedPayload: json: string -> obj

/// JS-085: one js-* tool invocation, end to end — the only orchestration of
/// sandbox execution, as-is per-write persistence, durable accounting and the
/// commit fact (JS-012/JS-013/JS-015). With `store = None` the workflow is
/// ephemeral (no durable facts — tests); with a store, the written mutations
/// are accounted as Prepared/Committed after the effects already landed.
module JsToolWorkflow =
    type FileAccessObservation = JsExplicitFileRead list -> string list -> Task<unit>

    /// Outcome of one invocation: the program's structured value plus the
    /// commit report — or a stable JsFailure. `Parked` is the top-level parked
    /// payload: the program suspended at `await js.park(payload)` and the site
    /// is registered under `handle` (PLAN S2 step 4).
    [<RequireQualifiedAccess>]
    type JsToolOutcome =
        | Succeeded of value: LlmFacing.Data.Value * rewritten: string list * created: string list * removed: string list * moved: (string * string) list
        | Failed of failure: JsFailure * rewritten: string list * created: string list * removed: string list * moved: (string * string) list
        | Parked of handle: string * payload: obj

    val run:
        capabilities: Set<JsCapability> ->
        root: string ->
        baseClassSource: string ->
        modelSource: string ->
        deadlineMs: int ->
        deadlineEpochMs: int64 ->
        outputBoundBytes: int ->
        persistence: IJsTransactionPersistence option ->
            Task<JsToolOutcome>

    val runWithFileAccessObservation:
        capabilities: Set<JsCapability> ->
        root: string ->
        baseClassSource: string ->
        modelSource: string ->
        deadlineMs: int ->
        deadlineEpochMs: int64 ->
        outputBoundBytes: int ->
        persistence: IJsTransactionPersistence option ->
        fileAccessObservation: FileAccessObservation ->
            Task<JsToolOutcome>

    /// Execute one js-* tool invocation under the three-state input protocol
    /// (PLAN S2 step 5): `{ program }` runs a new program, `{ unpark: {
    /// handle, value } }` resumes a parked site, `{ abandon: { handle } }`
    /// enters the ABANDONED protocol (algorithm F). `sessionId` tags the
    /// parked site so session close can reap it.
    val execute:
        sessionId: string ->
        capabilities: Set<JsCapability> ->
        root: string ->
        baseClassSource: string ->
        input: obj ->
        deadlineMs: int ->
        deadlineEpochMs: int64 ->
        outputBoundBytes: int ->
        persistence: IJsTransactionPersistence option ->
        lifecyclePersistence: IJsLifecyclePersistence option ->
        fileAccessObservation: FileAccessObservation option ->
            Task<JsToolOutcome>

/// Module-level parked-site table (PLAN S2 step 3, algorithms C/D/E/F/G).
/// The table is a process-live object: it resets on restart, never touches
/// durable storage, and consumes each handle exactly once with no tombstone.
/// The module is owned here (ToolWorkflow.fs) so ToolRegistry, PairHint and
/// PluginSessionScope can call `parkedHandles` / `reapSession` through the
/// OpenCode namespace without defining the table themselves (repository-
/// programming-031).
module JsParkedSites =
    /// A parked site: the in-flight sandbox run handle (whose closure chain
    /// holds the vm context and whose DeadlineRef is resettable), the read
    /// snapshots for write-before checks, the session owner, and the Parked
    /// state.
    type JsParkedSite =
        { Handle: string
          RunHandle: Wanxiangshu.Process.JsSandbox.JsRunHandle
          ReadSnapshots: ResizeArray<JsReadSnapshot>
          SessionId: string }

    /// Concurrency ceiling for parked sites (PLAN S2 step 3: default 8).
    val concurrencyLimit: int

    /// Allocate a monotonically increasing process-unique handle (algorithm C2).
    val allocateHandle: unit -> string

    val register:
        site: JsParkedSite -> Result<string, JsFailure>

    val tryFind: handle: string -> JsParkedSite option
    val remove: handle: string -> unit
    val parkedHandles: unit -> string list
    val reapSession: sessionId: string -> Task
    val clearAll: unit -> unit

/// JS-016: stable LLM-visible result shapes, rendered as Synthetic TOML
/// (the only rendering owner; ARCH-010). Success is `# ok` plus data/fs;
/// failure is `# failed` plus code/reason; a parked payload renders as a
/// top-level `parked` table (PLAN S2 step 4).
module JsToolsResult =
    val render: outcome: JsToolWorkflow.JsToolOutcome -> string
