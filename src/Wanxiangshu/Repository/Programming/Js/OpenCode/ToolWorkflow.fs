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
open FsToolkit.ErrorHandling
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

/// Parse the sandbox JSON string into LlmFacing reference data and enforce JS-010.
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

    let rec private isPrimitiveTree (value: LlmFacing.Data.Value) =
        match value with
        | LlmFacing.Data.Value.Bool _
        | LlmFacing.Data.Value.Integer _
        | LlmFacing.Data.Value.Float _
        | LlmFacing.Data.Value.String _ -> true
        | LlmFacing.Data.Value.Array items -> List.forall isPrimitiveTree items
        | LlmFacing.Data.Value.Null
        | LlmFacing.Data.Value.Object _ -> false

    let private validateArray (items: LlmFacing.Data.Value list) : Result<unit, JsFailure> =
        if
            items
            |> List.exists (function
                | LlmFacing.Data.Value.Null -> true
                | _ -> false)
        then
            Error JsFailure.InvalidReturnValue
        elif List.isEmpty items then
            Ok()
        elif
            items
            |> List.forall (function
                | LlmFacing.Data.Value.Object _ -> true
                | _ -> false)
        then
            Ok()
        elif List.forall isPrimitiveTree items then
            Ok()
        else
            Error JsFailure.InvalidReturnValue

    let private ofJsNumber (value: obj) : Result<LlmFacing.Data.Value, JsFailure> =
        if not (jsIsFinite value) then
            Error JsFailure.InvalidReturnValue
        elif not (jsIsInteger value) then
            Ok(LlmFacing.Data.Value.Float(unbox value))
        elif abs (unbox<float> value) <= maxSafeInteger then
            Ok(LlmFacing.Data.Value.Integer(int64 (unbox<float> value)))
        else
            Ok(LlmFacing.Data.Value.Float(unbox<float> value))

    let rec private ofJsValue (value: obj) : Result<LlmFacing.Data.Value, JsFailure> =
        let ty = jsType value

        if jsNull value then
            Ok LlmFacing.Data.Value.Null
        elif ty = "boolean" then
            Ok(LlmFacing.Data.Value.Bool(unbox value))
        elif ty = "string" then
            Ok(LlmFacing.Data.Value.String(unbox value))
        elif ty = "number" then
            ofJsNumber value
        elif jsIsArray value then
            ofJsArray value
        elif ty = "object" then
            ofJsObject value
        else
            Error JsFailure.InvalidReturnValue

    and ofJsArray (value: obj) : Result<LlmFacing.Data.Value, JsFailure> =
        result {
            let! items =
                [ 0 .. jsLength value - 1 ]
                |> List.traverseResultM (fun index -> ofJsValue (jsGet value index))

            do! validateArray items
            return LlmFacing.Data.Value.Array items
        }

    and ofJsObject (value: obj) : Result<LlmFacing.Data.Value, JsFailure> =
        result {
            let keys = jsKeys value

            let! fields =
                [ 0 .. keys.Length - 1 ]
                |> List.traverseResultM (fun index ->
                    ofJsValue (jsGet value keys.[index])
                    |> Result.map (fun item -> keys.[index], item))

            return LlmFacing.Data.Value.Object fields
        }

    let parse (json: string) : Result<LlmFacing.Data.Value, JsFailure> =
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

    type FileAccessObservation = string list -> string list -> Task<unit>

    /// Outcome of one invocation: the program's structured value plus the
    /// commit report — or a stable JsFailure.
    type JsToolOutcome =
        | Succeeded of value: LlmFacing.Data.Value * rewritten: string list * created: string list
        | Failed of JsFailure

    let private rewrittenPaths (mutations: JsStagedMutation list) =
        mutations
        |> List.choose (function
            | JsStagedMutation.Rewrite(path, _, _) -> Some path
            | JsStagedMutation.Create _ -> None)

    let private createdPaths (mutations: JsStagedMutation list) =
        mutations
        |> List.choose (function
            | JsStagedMutation.Create(path, _) -> Some path
            | JsStagedMutation.Rewrite _ -> None)

    let private readCurrentText (root: string) (path: string) : string option =
        JsUtf8Fs.readUtf8Classified (JsMutationFs.resolveToolPath root path)
        |> Result.toOption

    let private commitEphemeral
        (root: string)
        (mutations: JsStagedMutation list)
        (value: LlmFacing.Data.Value)
        : Result<LlmFacing.Data.Value * string list * string list, JsFailure> =
        result {
            do! JsMutationFs.commitPlan root (JsTransaction.commitPlan mutations)
            return value, rewrittenPaths mutations, createdPaths mutations
        }

    let private mapPrepareFailure (operation: Task<Result<'a, string>>) : Task<Result<'a, JsFailure>> =
        operation
        |> TaskValue.map (Result.mapError (fun _ -> JsFailure.TransactionPrepareFailed))

    let private mapCommitFailure (operation: Task<Result<'a, string>>) : Task<Result<'a, JsFailure>> =
        operation
        |> TaskValue.map (Result.mapError (fun _ -> JsFailure.TransactionCommitFailed))

    let private commitDurable
        (durable: IJsTransactionPersistence)
        (root: string)
        (mutations: JsStagedMutation list)
        (value: LlmFacing.Data.Value)
        (prepared: JsTransactionPrepared)
        : Task<Result<LlmFacing.Data.Value * string list * string list, JsFailure>> =
        taskResult {
            let! _ = durable.AppendPrepared prepared |> mapPrepareFailure
            do! JsMutationFs.commitPlan root (JsTransaction.commitPlan mutations)
            let! _ = durable.AppendCommitted prepared.TransactionId |> mapCommitFailure
            return value, rewrittenPaths mutations, createdPaths mutations
        }

    let private commitMutations
        (root: string)
        (mutations: JsStagedMutation list)
        (value: LlmFacing.Data.Value)
        (persistence: IJsTransactionPersistence option)
        : Task<Result<LlmFacing.Data.Value * string list * string list, JsFailure>> =
        let prepared =
            { TransactionId = JsTransactionId.generate ()
              WorkspaceRoot = root
              Mutations = JsTransactionFacts.ofStaged mutations }

        match persistence with
        | None -> commitEphemeral root mutations value |> Task.FromResult
        | Some durable -> commitDurable durable root mutations value prepared

    let private invokeObservation observe readPaths effectPaths : Task<Result<unit, JsFailure>> =
        task {
            try
                do! observe readPaths effectPaths
            with _ ->
                ()

            return Ok()
        }

    let private observeFileAccess
        (observer: FileAccessObservation option)
        readPaths
        effectPaths
        : Task<Result<unit, JsFailure>> =
        match observer with
        | None -> Task.FromResult(Ok())
        | Some observe -> invokeObservation observe readPaths effectPaths

    /// Run a model program against root. `baseClassSource` is the generated
    /// JsProgram (JS-002); `modelSource` is the model's `class Js ... run()`.
    /// deadlineMs bounds the sandbox; outputBoundBytes bounds the result.
    /// `persistence` enables durable prepare/commit facts (JS-012). Current and
    /// transaction head are owned by the canonical Integrator; no history reader is exposed here.
    let private runCore
        (root: string)
        (baseClassSource: string)
        (modelSource: string)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (outputBoundBytes: int)
        (persistence: IJsTransactionPersistence option)
        (fileAccessObservation: FileAccessObservation option)
        : Task<JsToolOutcome> =
        task {
            let! outcome =
                taskResult {
                    // DSL-MUTABLE: algorithm-scratch — JS mutation staging accumulator
                    let staging = ResizeArray<JsStagedMutation>()
                    let modelReads = ResizeArray<string>()
                    let api = JsToolsBindings.createApi root staging modelReads

                    let! resultJson =
                        JsSandbox.runSurface baseClassSource modelSource api deadlineMs deadlineEpochMs outputBoundBytes

                    let! value = JsToolsData.parse resultJson
                    let mutations = staging |> Seq.toList
                    let readPaths = modelReads |> Seq.distinct |> Seq.toList
                    let effectPaths = mutations |> List.map JsStagedMutation.path |> List.distinct

                    let exists (path: string) : bool =
                        JsMutationFs.existsPath (JsMutationFs.resolveToolPath root path)

                    do! JsTransaction.preflight exists (readCurrentText root) mutations

                    do! observeFileAccess fileAccessObservation readPaths effectPaths

                    if List.isEmpty mutations then
                        return value, [], []
                    else
                        return! commitMutations root mutations value persistence
                }

            match outcome with
            | Ok(value, rewritten, created) -> return Succeeded(value, rewritten, created)
            | Error failure -> return Failed failure
        }

    let run
        (root: string)
        (baseClassSource: string)
        (modelSource: string)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (outputBoundBytes: int)
        (persistence: IJsTransactionPersistence option)
        : Task<JsToolOutcome> =
        runCore root baseClassSource modelSource deadlineMs deadlineEpochMs outputBoundBytes persistence None

    let runWithFileAccessObservation
        (root: string)
        (baseClassSource: string)
        (modelSource: string)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (outputBoundBytes: int)
        (persistence: IJsTransactionPersistence option)
        (fileAccessObservation: FileAccessObservation)
        : Task<JsToolOutcome> =
        runCore
            root
            baseClassSource
            modelSource
            deadlineMs
            deadlineEpochMs
            outputBoundBytes
            persistence
            (Some fileAccessObservation)

/// JS-016: stable LLM-visible result shapes, rendered as Synthetic TOML
/// (the only rendering owner; ARCH-010). Success is `# ok` plus data/fs;
/// failure is `# failed` plus code/reason.
module JsToolsResult =

    let render (outcome: JsToolWorkflow.JsToolOutcome) : string =
        match outcome with
        | JsToolWorkflow.JsToolOutcome.Succeeded(value, rewritten, created) ->
            LlmFacing.instruction "ok"
            |> LlmFacing.withData (
                LlmFacing.Data.structuredValue value
                @ LlmFacing.Data.fileEffects rewritten created
            )
            |> LlmFacing.render
        | JsToolWorkflow.JsToolOutcome.Failed failure ->
            LlmFacing.instruction "failed"
            |> LlmFacing.withData
                [ LlmFacing.Data.stringField "code" (JsFailure.code failure)
                  LlmFacing.Data.stringField "reason" (JsFailure.reason failure) ]
            |> LlmFacing.render
