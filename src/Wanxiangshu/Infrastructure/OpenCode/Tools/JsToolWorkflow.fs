namespace Wanxiangshu.Infrastructure

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Process

/// JS-085: one js-* tool invocation, end to end — the only orchestration of
/// sandbox execution, staging, preflight and commit. Durable prepare facts
/// are NOT written here yet (Phase B-5 next): the workflow is fully
/// functional for ephemeral staging, and the commit is all-or-nothing.
module JsToolWorkflow =

    /// Outcome of one invocation: the program's JSON result plus the commit
    /// report (files written / files created) — or a stable JsFailure.
    type JsToolOutcome =
        | Succeeded of resultJson: string * written: string list * created: string list
        | Failed of JsFailure

    /// Run a model program against root. `baseClassSource` is the generated
    /// JsProgram (JS-002); `modelSource` is the model's `class Js ... run()`.
    /// deadlineMs bounds the sandbox; outputBoundBytes bounds the result.
    let run
        (root: string)
        (baseClassSource: string)
        (modelSource: string)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (outputBoundBytes: int)
        : Task<JsToolOutcome> =
        task {
            // 1. sandbox execution with staged-only bindings
            let staging = ResizeArray<JsStagedMutation>()
            let api = JsToolsBindings.createApi root staging

            let! sandboxResult =
                JsSandbox.runSurface baseClassSource modelSource api deadlineMs deadlineEpochMs outputBoundBytes

            match sandboxResult with
            | Error failure -> return Failed failure
            | Ok resultJson ->
                // 2. preflight: the pure transaction rules over live fs facts
                let mutations = staging |> Seq.toList

                let exists (path: string) : bool =
                    JsToolsFs.existsPath (JsToolsFs.resolveToolPath root path)

                let readCurrent (path: string) : string option =
                    match JsToolsFs.readUtf8Classified (JsToolsFs.resolveToolPath root path) with
                    | Ok text -> Some text
                    | Error _ -> None

                match JsTransaction.preflight exists readCurrent mutations with
                | Error failure -> return Failed failure
                | Ok() ->
                    // 3. commit: all-or-nothing
                    let plan = JsTransaction.commitPlan mutations

                    match JsToolsFs.commitPlan root plan with
                    | Error failure -> return Failed failure
                    | Ok() ->
                        let written =
                            mutations
                            |> List.choose (fun m ->
                                match m with
                                | JsStagedMutation.Rewrite(path, _, _) -> Some path
                                | JsStagedMutation.Create _ -> None)

                        let created =
                            mutations
                            |> List.choose (fun m ->
                                match m with
                                | JsStagedMutation.Create(path, _) -> Some path
                                | JsStagedMutation.Rewrite _ -> None)

                        return Succeeded(resultJson, written, created)
        }
