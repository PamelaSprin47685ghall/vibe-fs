namespace Wanxiangshu.Repository.Programming.Js

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type JsStagedMutation =
    | Rewrite of path: string * originalText: string * newText: string
    | Create of path: string * text: string

type JsReadSnapshot =
    { Path: string
      Text: string }

[<RequireQualifiedAccess>]
type JsCommitMutation =
    | RewriteFile of path: string * expectedCurrent: string * newText: string
    | CreateFile of path: string * newText: string

[<RequireQualifiedAccess>]
type JsRollbackMutation =
    | RestoreFile of path: string * expectedCurrent: string * originalText: string
    | RemoveCreatedFile of path: string * expectedCurrent: string

module JsStagedMutation =
    val path: mutation: JsStagedMutation -> string

module JsTransaction =
    val validateSingleIntent: mutations: JsStagedMutation list -> Result<JsStagedMutation list, JsFailure>
    val validateTargets:
        exists: (string -> bool) -> mutations: JsStagedMutation list -> Result<unit, JsFailure>
    val validateFreshness:
        readCurrent: (string -> string option) -> mutations: JsStagedMutation list -> Result<unit, JsFailure>
    val preflight:
        exists: (string -> bool) ->
        readCurrent: (string -> string option) ->
        readSnapshots: JsReadSnapshot list ->
        mutations: JsStagedMutation list ->
        Result<unit, JsFailure>
    val commitPlan: mutations: JsStagedMutation list -> JsCommitMutation list
    val rollbackPlan: mutations: JsStagedMutation list -> JsRollbackMutation list

type JsTransactionId = private JsTransactionId of string

module JsTransactionId =
    val create: value: string -> JsTransactionId
    val value: JsTransactionId -> string
    val generate: unit -> JsTransactionId

type JsDurableMutation =
    { Path: string
      OriginalText: string option
      NewText: string }

type JsTransactionPrepared =
    { TransactionId: JsTransactionId
      WorkspaceRoot: string
      Mutations: JsDurableMutation list }

type JsTransactionCommitted =
    { TransactionId: JsTransactionId }

type JsTransactionProjection =
    { Head: EventId option
      Pending: Map<JsTransactionId, JsTransactionPrepared> }

module JsTransactionProjection =
    val empty: JsTransactionProjection
    val prepared: eventId: EventId -> value: JsTransactionPrepared -> projection: JsTransactionProjection -> JsTransactionProjection
    val committed: eventId: EventId -> value: JsTransactionCommitted -> projection: JsTransactionProjection -> JsTransactionProjection
    val pending: projection: JsTransactionProjection -> JsTransactionPrepared list

module JsTransactionFacts =
    val ofStaged: mutations: JsStagedMutation list -> JsDurableMutation list
