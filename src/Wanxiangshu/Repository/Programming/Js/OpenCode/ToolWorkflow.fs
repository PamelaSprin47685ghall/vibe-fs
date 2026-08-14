namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open Wanxiangshu.OpenCode
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
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Process

/// Parse the sandbox JSON string into SyntheticToml.DataValue and enforce JS-010.
module JsToolsData =

    [<Emit("$0 === null")>]
    let private jsNull (value: obj) : bool = jsNative

    [<Emit("typeof $0")>]
    let private jsType (value: obj) : string = jsNative

    [<Emit("Array.isArray($0)")>]
    let private jsIsArray (value: obj) : bool = jsNative

    [<Emit("Object.keys($0)")>]
    let private jsKeys (value: obj) : string[] = jsNative

    [<Emit("$0[$1]")>]
    let private jsGet (value: obj) (key: obj) : obj = jsNative

    [<Emit("$0.length")>]
    let private jsLength (value: obj) : int = jsNative

    [<Emit("Number.isInteger($0)")>]
    let private jsIsInteger (value: obj) : bool = jsNative

    [<Emit("Number.isFinite($0)")>]
    let private jsIsFinite (value: obj) : bool = jsNative

    let private maxSafeInteger = 9007199254740991.0

    let rec private isPrimitiveTree (value: SyntheticToml.DataValue) =
        match value with
        | SyntheticToml.DataValue.Bool _
        | SyntheticToml.DataValue.Integer _
        | SyntheticToml.DataValue.Float _
        | SyntheticToml.DataValue.String _ -> true
        | SyntheticToml.DataValue.Array items -> List.forall isPrimitiveTree items
        | SyntheticToml.DataValue.Null
        | SyntheticToml.DataValue.Object _ -> false

    let private validateArray (items: SyntheticToml.DataValue list) : Result<unit, JsFailure> =
        if
            items
            |> List.exists (function
                | SyntheticToml.DataValue.Null -> true
                | _ -> false)
        then
            Error JsFailure.InvalidReturnValue
        elif List.isEmpty items then
            Ok()
        elif
            items
            |> List.forall (function
                | SyntheticToml.DataValue.Object _ -> true
                | _ -> false)
        then
            Ok()
        elif List.forall isPrimitiveTree items then
            Ok()
        else
            Error JsFailure.InvalidReturnValue

    let rec private ofJsValue (value: obj) : Result<SyntheticToml.DataValue, JsFailure> =
        if jsNull value then
            Ok SyntheticToml.DataValue.Null
        else
            match jsType value with
            | "boolean" -> Ok(SyntheticToml.DataValue.Bool(unbox value))
            | "string" -> Ok(SyntheticToml.DataValue.String(unbox value))
            | "number" ->
                if not (jsIsFinite value) then
                    Error JsFailure.InvalidReturnValue
                elif jsIsInteger value then
                    let n: float = unbox value

                    if abs n <= maxSafeInteger then
                        Ok(SyntheticToml.DataValue.Integer(int64 n))
                    else
                        Ok(SyntheticToml.DataValue.Float n)
                else
                    Ok(SyntheticToml.DataValue.Float(unbox value))
            | "object" when jsIsArray value ->
                let rec loop index acc =
                    if index = jsLength value then
                        let items = List.rev acc

                        match validateArray items with
                        | Error failure -> Error failure
                        | Ok() -> Ok(SyntheticToml.DataValue.Array items)
                    else
                        match ofJsValue (jsGet value index) with
                        | Error failure -> Error failure
                        | Ok item -> loop (index + 1) (item :: acc)

                loop 0 []
            | "object" ->
                let keys = jsKeys value

                let rec loop index acc =
                    if index = keys.Length then
                        Ok(SyntheticToml.DataValue.Object(List.rev acc))
                    else
                        match ofJsValue (jsGet value keys.[index]) with
                        | Error failure -> Error failure
                        | Ok item -> loop (index + 1) ((keys.[index], item) :: acc)

                loop 0 []
            | _ -> Error JsFailure.InvalidReturnValue

    let parse (json: string) : Result<SyntheticToml.DataValue, JsFailure> =
        try
            ofJsValue (JS.JSON.parse json)
        with _ ->
            Error JsFailure.InvalidReturnValue

/// JS-085: one js-* tool invocation, end to end — the only orchestration of
/// sandbox execution, staging, preflight, durable prepare, commit and the
/// commit fact (JS-012/JS-013/JS-015). With `store = None` the workflow is
/// ephemeral (no durable facts — tests); with a store, the transaction is
/// Prepared BEFORE any filesystem effect and Committed AFTER, so crash
/// recovery can undo only what was provably written.
module JsToolWorkflow =

    /// Outcome of one invocation: the program's structured value plus the
    /// commit report — or a stable JsFailure.
    type JsToolOutcome =
        | Succeeded of value: SyntheticToml.DataValue * rewritten: string list * created: string list
        | Failed of JsFailure

    /// Run a model program against root. `baseClassSource` is the generated
    /// JsProgram (JS-002); `modelSource` is the model's `class Js ... run()`.
    /// deadlineMs bounds the sandbox; outputBoundBytes bounds the result.
    /// `persistence` enables durable prepare/commit facts (JS-012). Current and
    /// transaction head are owned by the canonical Integrator; no history reader is exposed here.
    let run
        (root: string)
        (baseClassSource: string)
        (modelSource: string)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (outputBoundBytes: int)
        (persistence: IJsTransactionPersistence option)
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
                match JsToolsData.parse resultJson with
                | Error failure -> return Failed failure
                | Ok value ->
                    // 2. preflight: the pure transaction rules over live fs facts
                    let mutations = staging |> Seq.toList

                    let exists (path: string) : bool =
                        JsMutationFs.existsPath (JsMutationFs.resolveToolPath root path)

                    let readCurrent (path: string) : string option =
                        match JsUtf8Fs.readUtf8Classified (JsMutationFs.resolveToolPath root path) with
                        | Ok text -> Some text
                        | Error _ -> None

                    match JsTransaction.preflight exists readCurrent mutations with
                    | Error failure -> return Failed failure
                    | Ok() when List.isEmpty mutations ->
                        // pure query: no transaction, no durable facts (JS-085)
                        return Succeeded(value, [], [])
                    | Ok() ->
                        // 3. durable prepare BEFORE any filesystem effect (JS-012)
                        let prepared =
                            { TransactionId = JsTransactionId.generate ()
                              WorkspaceRoot = root
                              Mutations = JsTransactionFacts.ofStaged mutations }

                        match persistence with
                        | None ->
                            // ephemeral path (tests / no store available)
                            match JsMutationFs.commitPlan root (JsTransaction.commitPlan mutations) with
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

                                return Succeeded(value, written, created)
                        | Some durable ->
                            match! durable.AppendPrepared prepared with
                            | Error _ -> return Failed JsFailure.TransactionPrepareFailed
                            | Ok preparedEventId ->
                                // 4. commit: all-or-nothing
                                match JsMutationFs.commitPlan root (JsTransaction.commitPlan mutations) with
                                | Error failure -> return Failed failure
                                | Ok() ->
                                    // 5. the commit fact (JS-012): its absence after
                                    // Prepared is what recovery uses to undo
                                    match! durable.AppendCommitted prepared.TransactionId with
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

                                        return Succeeded(value, written, created)
        }

/// JS-016: stable LLM-visible result shapes, rendered as Synthetic TOML
/// (the only rendering owner; ARCH-010). Success is `# ok` plus data/fs;
/// failure is `# failed` plus code/reason.
module JsToolsResult =

    let render (outcome: JsToolWorkflow.JsToolOutcome) : string =
        match outcome with
        | JsToolWorkflow.JsToolOutcome.Succeeded(value, rewritten, created) ->
            SyntheticToml.document [ "ok" ] (SyntheticToml.encodeData value @ SyntheticToml.encodeFs rewritten created)
        | JsToolWorkflow.JsToolOutcome.Failed failure ->
            SyntheticToml.document
                [ "failed" ]
                [ SyntheticToml.field "code" (SyntheticToml.renderString (JsFailure.code failure))
                  SyntheticToml.field "reason" (SyntheticToml.renderString (JsFailure.reason failure)) ]
