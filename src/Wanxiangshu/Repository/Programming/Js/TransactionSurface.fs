// primary_owner: repository-programming — RepoProgramming.TransactionSurface (REPOSITORY-PROGRAMMING-010) — KEEP — Js transaction commit/rollback, caller WorkflowSurface via surface
namespace Wanxiangshu.Repository.Programming.Js

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Persistence.EventStore

/// JS-native owner boundary for the transaction decision algebra and its one
/// durable EventStore adapter. Mutation records, failures and projections are
/// translated here; Fable unions and lists never cross this edge.
[<RequireQualifiedAccess>]
module JsTransactionSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private failureResult failure =
        box
            {| ok = false
               code = JsFailure.code failure
               reason = JsFailure.reason failure |}

    let private unitResult result =
        match result with
        | Ok _ -> box {| ok = true |}
        | Error failure -> failureResult failure

    let private mutationOf (value: obj) : JsStagedMutation =
        match text (value?kind) with
        | "rewrite" -> JsStagedMutation.Rewrite(text (value?path), text (value?originalText), text (value?newText))
        | "create" -> JsStagedMutation.Create(text (value?path), text (value?text))
        | other -> invalidArg "kind" (sprintf "unknown staged mutation kind: %s" other)

    let private mutationsOf (value: obj) : JsStagedMutation list =
        if isNull value then
            []
        else
            unbox<obj array> value |> Array.toList |> List.map mutationOf

    let private declarationOf (value: obj) : AnchorDeclaration =
        let spec =
            match text (value?kind) with
            | "exact" -> AnchorSpec.Exact(text (value?text))
            | "regex" -> AnchorSpec.Regex(text (value?text))
            | other -> invalidArg "kind" (sprintf "unknown anchor kind: %s" other)

        let occurrence =
            if isNull value || isNull (value?occurrence) then
                None
            else
                Some(int (value?occurrence))

        { Spec = spec; Occurrence = occurrence }

    let private optionText (value: obj) =
        if isNull value then None else Some(string value)

    /// Stable failure catalog; constructor identity remains private to the
    /// domain while every shipped code and reason is observable as plain data.
    let failureCatalog () : obj array =
        [| JsFailure.InvalidProgram, "InvalidProgram"
           JsFailure.ProgramFailed "", "ProgramFailed"
           JsFailure.ProgramTimeout, "ProgramTimeout"
           JsFailure.ProgramResourceLimit, "ProgramResourceLimit"
           JsFailure.FileNotFound "a", "FileNotFound"
           JsFailure.FileAlreadyExists "a", "FileAlreadyExists"
           JsFailure.InvalidUtf8 "a", "InvalidUtf8"
           JsFailure.AnchorNotFound "missing", "AnchorNotFound"
           JsFailure.AnchorNotUnique, "AnchorNotUnique"
           JsFailure.DuplicateMutationTarget "a", "DuplicateMutationTarget"
           JsFailure.ResultTooLarge None, "ResultTooLarge"
           JsFailure.InvalidReturnValue, "InvalidReturnValue"
           JsFailure.FileChanged "a", "FileChanged"
           JsFailure.TransactionPrepareFailed, "TransactionPrepareFailed"
           JsFailure.TransactionCommitFailed, "TransactionCommitFailed"
           JsFailure.TransactionRecoveryRequired, "TransactionRecoveryRequired"
           JsFailure.UnknownMember, "UnknownMember" |]
        |> Array.map (fun (failure, name) ->
            box
                {| name = name
                   code = JsFailure.code failure
                   reason = JsFailure.reason failure |})

    let validateAnchorDeclaration (declaration: obj) : obj =
        AnchorRules.validateDeclaration (declarationOf declaration) |> unitResult

    let validateAnchorOccurrence (declaration: obj) : obj =
        AnchorRules.validateOccurrence (declarationOf declaration) |> unitResult

    let validateSingleIntent (mutations: obj array) : obj =
        JsTransaction.validateSingleIntent (mutationsOf (box mutations)) |> unitResult

    let private existsOf (paths: string array) path = paths |> Array.contains path

    let validateTargets (existing: string array) (mutations: obj array) : obj =
        JsTransaction.validateTargets (existsOf existing) (mutationsOf (box mutations))
        |> unitResult

    let private currentOf (current: obj) path =
        if isNull current then None else optionText (current?(path))

    let validateFreshness (current: obj) (mutations: obj array) : obj =
        JsTransaction.validateFreshness (currentOf current) (mutationsOf (box mutations))
        |> unitResult

    let preflight (existing: string array) (current: obj) (mutations: obj array) : obj =
        JsTransaction.preflight (existsOf existing) (currentOf current) (mutationsOf (box mutations))
        |> unitResult

    let private pairToJs (path, value) = box [| box path; box value |]

    let commitPlan (mutations: obj array) : obj array =
        JsTransaction.commitPlan (mutationsOf (box mutations))
        |> List.map pairToJs
        |> List.toArray

    let rollbackPlan (mutations: obj array) : obj array =
        JsTransaction.rollbackPlan (mutationsOf (box mutations))
        |> List.map (fun (path, value) -> box [| box path; value |> Option.map box |> Option.toObj |])
        |> List.toArray

    let private eventStoreOf (handle: obj) : IEventStore = (unbox<EventStoreHandle> handle).Store

    let private durableMutationToJs (mutation: JsDurableMutation) =
        box
            {| path = mutation.Path
               originalText = mutation.OriginalText |> Option.toObj
               newText = mutation.NewText |}

    let private preparedToJs (prepared: JsTransactionPrepared) =
        box
            {| transactionId = JsTransactionId.value prepared.TransactionId
               workspaceRoot = prepared.WorkspaceRoot
               mutations = prepared.Mutations |> List.map durableMutationToJs |> List.toArray |}

    let private preparedOf (value: obj) : JsTransactionPrepared =
        let mutations =
            if isNull value?mutations then
                []
            else
                unbox<obj array> value?mutations
                |> Array.toList
                |> List.map (fun item ->
                    { Path = text (item?path)
                      OriginalText =
                        if isNull (item?originalText) then
                            None
                        else
                            Some(text (item?originalText))
                      NewText = text (item?newText) })

        { TransactionId = JsTransactionId.create (text (value?transactionId))
          WorkspaceRoot = text (value?workspaceRoot)
          Mutations = mutations }

    let private appendResult result =
        match result with
        | Ok _ -> box {| ok = true |}
        | Error message -> box {| ok = false; error = message |}

    /// Append Prepared through the canonical EventStore Current integrator.
    let appendPrepared (store: obj) (prepared: obj) : Task<obj> =
        task {
            let! result = JsToolsTransactionStore.appendPrepared (eventStoreOf store) (preparedOf prepared)
            return appendResult result
        }

    /// Append Committed through the same transaction stream.
    let appendCommitted (store: obj) (transactionId: string) : Task<obj> =
        task {
            let! result =
                JsToolsTransactionStore.appendCommitted (eventStoreOf store) (JsTransactionId.create transactionId)

            return appendResult result
        }

    /// Observe only the Integrator-owned pending projection; no history reader
    /// or recovery mutation is exposed.
    let pending (store: obj) : obj array =
        match eventStoreOf store |> fun value -> value.TryCurrent("JsTransaction") with
        | None -> [||]
        | Some value ->
            JsTransactionProjection.pending (unbox<JsTransactionProjection> value)
            |> List.map preparedToJs
            |> List.toArray

    /// Opaque persistence capability consumed by JsWorkflowSurface.
    let internal persistenceOf (store: obj) : IJsTransactionPersistence =
        JsToolsTransactionStore.createPersistence (eventStoreOf store)

    let createPersistence (store: obj) : obj = persistenceOf store |> box
