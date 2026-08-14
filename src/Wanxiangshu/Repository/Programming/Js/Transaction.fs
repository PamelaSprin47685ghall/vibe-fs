namespace Wanxiangshu.Repository.Programming.Js

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
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

open Wanxiangshu.Foundation.Identity

/// DSL-class: DurableFact — JS-012/JS-013/JS-015: one staged mutation in a
/// js-* program. `Rewrite` edits an existing file; `Create` writes a missing
/// one (JS-008/009). The set of all mutations is the WriteSet; it commits
/// all-or-nothing or not at all.
[<RequireQualifiedAccess>]
type JsStagedMutation =
    | Rewrite of path: string * originalText: string * newText: string
    | Create of path: string * text: string

module JsStagedMutation =

    let path (mutation: JsStagedMutation) : string =
        match mutation with
        | JsStagedMutation.Rewrite(path, _, _) -> path
        | JsStagedMutation.Create(path, _) -> path

/// DSL-class: Decision — JS-012/013/014: the pure transaction rules. The
/// filesystem facts (existence, current content) are injected so the decision
/// stays deterministic and testable without Host I/O; the commit/rollback
/// side effects are the adapter's job, never this module's.
module JsTransaction =

    /// JS-026 same-path-once: one program may mutate each path exactly once.
    let validateSingleIntent (mutations: JsStagedMutation list) : Result<JsStagedMutation list, JsFailure> =
        let duplicates =
            mutations
            |> List.countBy JsStagedMutation.path
            |> List.tryFind (fun (_, count) -> count > 1)

        match duplicates with
        | Some(path, _) -> Error(JsFailure.DuplicateMutationTarget path)
        | None -> Ok mutations

    /// JS-008/009: rewrite targets must exist, create targets must not.
    let validateTargets (exists: string -> bool) (mutations: JsStagedMutation list) : Result<unit, JsFailure> =
        mutations
        |> List.tryPick (fun mutation ->
            match mutation with
            | JsStagedMutation.Rewrite(path, _, _) when not (exists path) -> Some(JsFailure.FileNotFound path)
            | JsStagedMutation.Create(path, _) when exists path -> Some(JsFailure.FileAlreadyExists path)
            | _ -> None)
        |> function
            | Some failure -> Error failure
            | None -> Ok()

    /// JS-014: a rewrite whose original text no longer matches the current
    /// file content is a conflict; no implicit retry. Create targets have no
    /// freshness constraint (they must be absent — enforced by validateTargets).
    let validateFreshness
        (readCurrent: string -> string option)
        (mutations: JsStagedMutation list)
        : Result<unit, JsFailure> =
        mutations
        |> List.tryPick (fun mutation ->
            match mutation with
            | JsStagedMutation.Rewrite(path, originalText, _) ->
                if readCurrent path = Some originalText then
                    None
                else
                    Some(JsFailure.FileChanged path)
            | JsStagedMutation.Create _ -> None)
        |> function
            | Some failure -> Error failure
            | None -> Ok()

    /// JS-013: full preflight — every rule must pass before any file effect.
    let preflight
        (exists: string -> bool)
        (readCurrent: string -> string option)
        (mutations: JsStagedMutation list)
        : Result<unit, JsFailure> =
        validateSingleIntent mutations
        |> Result.bind (fun validated ->
            validateTargets exists validated
            |> Result.bind (fun () -> validateFreshness readCurrent validated))

    /// JS-013: the commit plan — one write per mutation, in declaration order.
    /// The adapter applies this plan; a failure anywhere rolls the whole plan
    /// back (rollback restores each original text).
    let commitPlan (mutations: JsStagedMutation list) : (string * string) list =
        mutations
        |> List.map (fun mutation ->
            match mutation with
            | JsStagedMutation.Rewrite(path, _, newText) -> path, newText
            | JsStagedMutation.Create(path, text) -> path, text)

    /// JS-015: rollback plan — restore every original text (rewrites only;
    /// creates are removed by the adapter). Order is reversed so a partial
    /// commit unwinds last-write-first.
    let rollbackPlan (mutations: JsStagedMutation list) : (string * string option) list =
        mutations
        |> List.rev
        |> List.map (fun mutation ->
            match mutation with
            | JsStagedMutation.Rewrite(path, originalText, _) -> path, Some originalText
            | JsStagedMutation.Create(path, _) -> path, None)

/// DSL-class: DurableFact — JS-012/015: identity of one js-* transaction.
type JsTransactionId = private JsTransactionId of string

module JsTransactionId =

    let create (value: string) = JsTransactionId value
    let value (JsTransactionId v) = v

    let generate () =
        JsTransactionId(System.Guid.NewGuid().ToString("N"))

/// DSL-class: DurableFact — JS-015: one mutation as persisted in a prepared
/// transaction, sufficient to undo it after a crash (original text for
/// rewrites, absence for creates).
type JsDurableMutation =
    {
        Path: string
        /// Some = rewrite (rollback restores this text); None = create.
        OriginalText: string option
        NewText: string
    }

/// DSL-class: DurableFact — JS-012: the durable prepare fact. Written to the
/// unified EventStore BEFORE any filesystem effect; a committed transaction
/// is a pair (Prepared, Committed) on the same stream.
type JsTransactionPrepared =
    { TransactionId: JsTransactionId
      WorkspaceRoot: string
      Mutations: JsDurableMutation list }

/// DSL-class: DurableFact — JS-012: the durable commit fact. Its presence
/// after a Prepared fact is what makes the transaction committed.
type JsTransactionCommitted = { TransactionId: JsTransactionId }

/// Incremental recovery Current owned by the canonical Integrator. Pending holds
/// exactly Prepared facts not yet followed by their matching Committed fact;
/// Head is the current EventStore stream parent for the next transaction fact.
type JsTransactionProjection =
    { Head: EventId option
      Pending: Map<JsTransactionId, JsTransactionPrepared> }

module JsTransactionProjection =
    let empty = { Head = None; Pending = Map.empty }

    let prepared (eventId: EventId) (value: JsTransactionPrepared) (projection: JsTransactionProjection) =
        { Head = Some eventId
          Pending = Map.add value.TransactionId value projection.Pending }

    let committed (eventId: EventId) (value: JsTransactionCommitted) (projection: JsTransactionProjection) =
        { Head = Some eventId
          Pending = Map.remove value.TransactionId projection.Pending }

    let pending projection =
        projection.Pending |> Map.toList |> List.map snd

module JsTransactionFacts =

    /// Durable mutations from a staged set (JS-012).
    let ofStaged (mutations: JsStagedMutation list) : JsDurableMutation list =
        mutations
        |> List.map (fun mutation ->
            match mutation with
            | JsStagedMutation.Rewrite(path, originalText, newText) ->
                { Path = path
                  OriginalText = Some originalText
                  NewText = newText }
            | JsStagedMutation.Create(path, text) ->
                { Path = path
                  OriginalText = None
                  NewText = text })
