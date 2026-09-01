namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Repository.Programming.Js

/// Parse the sandbox JSON string into LlmFacing reference data and enforce JS-010.
module JsToolsData =
    val parse: json: string -> Result<LlmFacing.Data.Value, JsFailure>

/// JS-085: one js-* tool invocation, end to end — the only orchestration of
/// sandbox execution, staging, preflight, durable prepare, commit and the
/// commit fact (JS-012/JS-013/JS-015). With `store = None` the workflow is
/// ephemeral (no durable facts — tests); with a store, the transaction is
/// Prepared BEFORE any filesystem effect and Committed AFTER, so crash
/// recovery can undo only what was provably written.
module JsToolWorkflow =
    type FileAccessObservation = string list -> string list -> Task<unit>

    /// Outcome of one invocation: the program's structured value plus the
    /// commit report — or a stable JsFailure.
    [<RequireQualifiedAccess>]
    type JsToolOutcome =
        | Succeeded of value: LlmFacing.Data.Value * rewritten: string list * created: string list
        | Failed of JsFailure

    val run:
        root: string ->
        baseClassSource: string ->
        modelSource: string ->
        deadlineMs: int ->
        deadlineEpochMs: int64 ->
        outputBoundBytes: int ->
        persistence: IJsTransactionPersistence option ->
            Task<JsToolOutcome>

    val runWithFileAccessObservation:
        root: string ->
        baseClassSource: string ->
        modelSource: string ->
        deadlineMs: int ->
        deadlineEpochMs: int64 ->
        outputBoundBytes: int ->
        persistence: IJsTransactionPersistence option ->
        fileAccessObservation: FileAccessObservation ->
            Task<JsToolOutcome>

/// JS-016: stable LLM-visible result shapes, rendered as Synthetic TOML
/// (the only rendering owner; ARCH-010). Success is `# ok` plus data/fs;
/// failure is `# failed` plus code/reason.
module JsToolsResult =
    val render: outcome: JsToolWorkflow.JsToolOutcome -> string
