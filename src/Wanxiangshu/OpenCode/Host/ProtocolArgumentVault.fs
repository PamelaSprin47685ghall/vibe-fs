namespace Wanxiangshu.OpenCode.Host

open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Strength

/// host-boundary-032: process-local vault of the protocol
/// fields exactly as the model produced them on the wire.
///
/// The Host serializes persisted tool-call arguments after tool.execute.before
/// has stripped the protocol fields, so the history the next provider request
/// is built from has already lost them. tool.execute.before is the only
/// in-process moment where the original bytes are observable; this vault keeps
/// a snapshot keyed by (sessionId, callId) so the provider-facing messages
/// transform can restore the fields into the request. Snapshot values are the
/// Host's own wire originals, never a second truth: the vault is a
/// process-local artifact (managed-chat-execution-009), dropped on dispose,
/// and fail-open when a call has no entry. It never decides whether a field
/// belongs to the protocol; the caller records exactly what its hide step
/// strips.
module ProtocolArgumentVault =

    [<Literal>]
    let private contractField = "contract"

    let private roundsField = InvestigationEstimateContract.EstimatedReadonlyRoundsField

    [<Literal>]
    let private noteField = "self_note"

    type FieldOwnership =
        { ReviewContract: bool
          InvestigationEstimate: bool }

    /// One snapshot of the protocol fields, verbatim from the tool arguments.
    type Snapshot =
        { Contract: obj option
          ReadonlyRounds: obj option
          SelfNote: obj option }

    type Vault =
        private
            { Entries: Dictionary<string, Snapshot> }

    let create () : Vault =
        { Entries = Dictionary<string, Snapshot>() }

    let private keyOf (sessionId: string) (callId: string) : string = sessionId + "\u001f" + callId

    let record (vault: Vault) (sessionId: string) (callId: string) (snapshot: Snapshot) : unit =
        vault.Entries[keyOf sessionId callId] <- snapshot

    let tryFind (vault: Vault) (sessionId: string) (callId: string) : Snapshot option =
        match vault.Entries.TryGetValue(keyOf sessionId callId) with
        | true, snapshot -> Some snapshot
        | false, _ -> None

    let clear (vault: Vault) : unit = vault.Entries.Clear()

    [<Emit("Object.prototype.hasOwnProperty.call($0, $1)")>]
    let private hasOwn (target: obj) (key: string) : bool = jsNative

    [<Emit("Object.is($0, $1)")>]
    let private sameValue (left: obj) (right: obj) : bool = jsNative

    [<Emit("{ ...$0 }")>]
    let private shallowCopy (source: obj) : obj = jsNative

    let private fieldMatches (current: obj) (field: string) (expected: obj) : bool =
        hasOwn current field && sameValue current?(field) expected

    /// Snapshot the protocol fields from raw tool arguments. Returns None when
    /// no protocol field is present; a call that never carried the protocol
    /// needs no vault entry.
    let private tryReadField owned (args: obj) (fieldName: string) =
        if owned && hasOwn args fieldName then
            Some(args?(fieldName))
        else
            None

    let private toSnapshot (contract: obj option) (rounds: obj option) (note: obj option) =
        match contract, rounds, note with
        | None, None, None -> None
        | _ ->
            Some
                { Contract = contract
                  ReadonlyRounds = rounds
                  SelfNote = note }

    /// Read only fields owned by this call's protocols. Unowned fields are
    /// not inspected; absent owned fields need no vault entry.
    let snapshotOfArguments (ownership: FieldOwnership) (args: obj) : Snapshot option =
        if isNull args then
            None
        else
            let contract = tryReadField ownership.ReviewContract args contractField
            let rounds = tryReadField ownership.InvestigationEstimate args roundsField
            let note = tryReadField ownership.InvestigationEstimate args noteField
            toSnapshot contract rounds note

    /// Pure merge decision: the arguments object the wire should carry.
    /// Business arguments are copied verbatim in their original order; protocol
    /// fields are appended only when missing or different. Returns the same
    /// reference when everything is already in place, so repeated transforms
    /// are idempotent and touch nothing.
    let private applyPendingFields (current: obj) (fields: (string * obj) list) =
        let merged = shallowCopy current

        for field, value in fields do
            merged?(field) <- value

        merged

    let private mergePendingFields (current: obj) (pending: (string * obj) list) =
        match pending with
        | [] -> current
        | fields -> applyPendingFields current fields

    let restoreArguments (snapshot: Snapshot) (current: obj) : obj =
        if isNull current then
            current
        else
            let pending =
                [ if
                      snapshot.Contract.IsSome
                      && not (fieldMatches current contractField snapshot.Contract.Value)
                  then
                      yield contractField, snapshot.Contract.Value
                  if
                      snapshot.ReadonlyRounds.IsSome
                      && not (fieldMatches current roundsField snapshot.ReadonlyRounds.Value)
                  then
                      yield roundsField, snapshot.ReadonlyRounds.Value
                  if
                      snapshot.SelfNote.IsSome
                      && not (fieldMatches current noteField snapshot.SelfNote.Value)
                  then
                      yield noteField, snapshot.SelfNote.Value ]

            mergePendingFields current pending
