namespace Wanxiangshu.Infrastructure

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Persist
open Wanxiangshu.Process

/// JS-085: one js-* tool invocation, end to end — the only orchestration of
/// sandbox execution, staging, preflight, durable prepare, commit and the
/// commit fact (JS-012/JS-013/JS-015). With `store = None` the workflow is
/// ephemeral (no durable facts — tests); with a store, the transaction is
/// Prepared BEFORE any filesystem effect and Committed AFTER, so crash
/// recovery can undo only what was provably written.
module JsToolWorkflow =

    /// Outcome of one invocation: the program's JSON result plus the commit
    /// report (files written / files created) — or a stable JsFailure.
    type JsToolOutcome =
        | Succeeded of resultJson: string * written: string list * created: string list
        | Failed of JsFailure

    /// Run a model program against root. `baseClassSource` is the generated
    /// JsProgram (JS-002); `modelSource` is the model's `class Js ... run()`.
    /// deadlineMs bounds the sandbox; outputBoundBytes bounds the result.
    /// `persistence` enables the durable prepare/commit facts (JS-012): the
    /// IEventStore appends facts; the IGitRawStore reads them back (merge).
    let run
        (root: string)
        (baseClassSource: string)
        (modelSource: string)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (outputBoundBytes: int)
        (persistence: (IEventStore * IGitRawStore) option)
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
                | Ok() when List.isEmpty mutations ->
                    // pure query: no transaction, no durable facts (JS-085)
                    return Succeeded(resultJson, [], [])
                | Ok() ->
                    // 3. durable prepare BEFORE any filesystem effect (JS-012)
                    let prepared =
                        { TransactionId = JsTransactionId.generate ()
                          WorkspaceRoot = root
                          Mutations = JsTransactionFacts.ofStaged mutations }

                    match persistence with
                    | None ->
                        // ephemeral path (tests / no store available)
                        match JsToolsFs.commitPlan root (JsTransaction.commitPlan mutations) with
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
                    | Some(eventStore, raw) ->
                        let snapshot = eventStore.OpenSnapshot()

                        let head =
                            match JsToolsTransactionStore.loadEvents raw snapshot with
                            | Ok events -> JsToolsTransactionStore.streamHead events
                            | Error _ -> None

                        match JsToolsTransactionStore.appendPrepared eventStore (Option.toList head) prepared with
                        | Error _ -> return Failed JsFailure.TransactionPrepareFailed
                        | Ok preparedEventId ->
                            // 4. commit: all-or-nothing
                            match JsToolsFs.commitPlan root (JsTransaction.commitPlan mutations) with
                            | Error failure -> return Failed failure
                            | Ok() ->
                                // 5. the commit fact (JS-012): its absence after
                                // Prepared is what recovery uses to undo
                                match
                                    JsToolsTransactionStore.appendCommitted
                                        eventStore
                                        [ preparedEventId ]
                                        prepared.TransactionId
                                with
                                | Error _ -> return Failed JsFailure.TransactionCommitFailed
                                | Ok _ ->
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

/// JS-016/JS-078.1: stable LLM-visible result shapes, rendered as Synthetic
/// TOML (the only rendering owner; ARCH-010). Success carries the program
/// JSON plus the commit report; failure carries the stable code + reason.
module JsToolsResult =

    let render (outcome: JsToolWorkflow.JsToolOutcome) : string =
        match outcome with
        | JsToolWorkflow.JsToolOutcome.Succeeded(resultJson, written, created) ->
            SyntheticToml.document
                []
                [ SyntheticToml.field "status" (SyntheticToml.renderString "ok")
                  SyntheticToml.field "result" (SyntheticToml.renderString resultJson)
                  SyntheticToml.field "written" (SyntheticToml.renderString (String.concat "," written))
                  SyntheticToml.field "created" (SyntheticToml.renderString (String.concat "," created)) ]
        | JsToolWorkflow.JsToolOutcome.Failed failure ->
            SyntheticToml.document
                []
                [ SyntheticToml.field "status" (SyntheticToml.renderString "failed")
                  SyntheticToml.field "code" (SyntheticToml.renderString (JsFailure.code failure))
                  SyntheticToml.field "reason" (SyntheticToml.renderString (JsFailure.reason failure)) ]
