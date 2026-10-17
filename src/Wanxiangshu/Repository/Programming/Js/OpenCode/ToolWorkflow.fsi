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
        | Succeeded of
            value: LlmFacing.Data.Value *
            rewritten: string list *
            created: string list *
            removed: string list *
            moved: (string * string) list
        | Failed of
            failure: JsFailure *
            rewritten: string list *
            created: string list *
            removed: string list *
            moved: (string * string) list
        | Parked of handle: string * payload: obj * source: string

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

/// JS-016: stable LLM-visible result shapes, rendered as Synthetic TOML
/// (the only rendering owner; ARCH-010). Success is `# ok` plus data/fs;
/// failure is `# failed` plus code/reason; a parked payload renders as a
/// top-level `parked` table (PLAN S2 step 4).
module JsToolsResult =
    val render: outcome: JsToolWorkflow.JsToolOutcome -> string
