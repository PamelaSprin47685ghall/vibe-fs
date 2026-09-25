namespace Wanxiangshu.Participant.Cognition

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// One committed call's terminal outcome.
///
/// `Replayed` is the frozen first result, not a second execution: the caller must not
/// run jq again, because a non-deterministic program (`now`, `input`) would then
/// produce different bytes for a fact that is already committed.
[<RequireQualifiedAccess>]
type CommitOutcome =
    | Committed of ordinal: int64
    | Replayed of ordinal: int64
    | Rejected of reason: string

[<RequireQualifiedAccess>]
module CommitInternals =

    /// The fact for a brand-new phase. `RendererVersion` is a presentation generation,
    /// not the snapshot's shape version: the committed bytes must not change when the
    /// renderer is upgraded, only the rendering does.
    let newCommit
        (ownerKey: string)
        (owner: CognitiveOwner.T)
        (toolCallId: ToolCallId)
        (ordinal: int64)
        (inputDigest: string)
        (snapshotRef: BlobRef)
        (snapshotDigest: BlobDigest)
        : AssumePhaseCommitted =
        { OwnerKey = ownerKey
          SessionId = owner.SessionId
          IncumbencyId = owner.IncumbencyId
          ToolCallId = toolCallId
          Ordinal = ordinal
          InputDigest = inputDigest
          PredecessorOrdinal = if ordinal = 1L then None else Some(ordinal - 1L)
          SnapshotRef = snapshotRef
          SnapshotDigest = snapshotDigest
          RendererVersion = "1" }

    /// The outcome of meeting a call the projection already holds.
    ///
    /// A replay with the same input answers with the frozen result; a replay with a
    /// different input is a typed identity conflict and must not pick a winner.
    let replayOutcome (state: CognitiveProjection) (toolCallId: ToolCallId) (inputDigest: string) : CommitOutcome =
        if state.LastInputDigest = Some inputDigest then
            CommitOutcome.Replayed state.Ordinal
        else
            CommitOutcome.Rejected(
                sprintf "assume call %s was already committed with different input" (ToolCallId.value toolCallId)
            )

    /// Persist one brand-new phase: blob first, then the fact.
    ///
    /// The order is the contract — a blob without a fact is an orphan the projection
    /// must never read, and a fact without a blob is a commit that cannot be rendered.
    let persistPhase
        (port: CognitiveJournalPort)
        (ownerKey: string)
        (owner: CognitiveOwner.T)
        (toolCallId: ToolCallId)
        (ordinal: int64)
        (inputDigest: string)
        (canvasJson: string)
        (todos: (string * TodoStatus * TodoPriority) list)
        : Task<CommitOutcome> =
        // The blob carries the snapshot, not the TOML rendering: the rendering is a
        // presentation decision the renderer version may change, while the committed
        // canvas must not.
        let snapshot = AssumeSnapshot.ofJson canvasJson todos

        let commit (snapshotRef: BlobRef) (snapshotDigest: BlobDigest) =
            newCommit ownerKey owner toolCallId ordinal inputDigest snapshotRef snapshotDigest

        /// Append the fact the blob just proved. A refusal here is an append failure,
        /// never a missing payload — the blob is already durable.
        let appendProven (snapshotRef: BlobRef) (snapshotDigest: BlobDigest) =
            task {
                match! port.AppendCommit(commit snapshotRef snapshotDigest) with
                | Error reason -> return Error reason
                | Ok() -> return Ok ordinal
            }

        /// Append the fact the blob just proved, reporting the committed ordinal.
        let appendAndCommit (snapshotRef: BlobRef) (snapshotDigest: BlobDigest) =
            task {
                match! appendProven snapshotRef snapshotDigest with
                | Error reason -> return CommitOutcome.Rejected reason
                | Ok committed -> return CommitOutcome.Committed committed
            }

        task {
            match! port.WriteBlob(AssumeSnapshot.json snapshot) with
            | Error reason -> return CommitOutcome.Rejected reason
            | Ok(snapshotRef, snapshotDigest) -> return! appendAndCommit snapshotRef snapshotDigest
        }

    /// The ordinal an owner's next committed phase claims. The committed ordinal is the
    /// only source: a process-local counter would restart at one after a reboot and let
    /// a fresh process re-claim an ordinal the journal already holds.
    let ordinalAfter (committed: CognitiveProjection option) : int64 =
        match committed with
        | Some state -> state.Ordinal + 1L
        | None -> 1L

/// Owner-scoped serial admission.
///
/// One queue per owner, so two owners never block each other, and a failed jq never
/// poisons the queue for the next legal call. Cross-instance sharing is a wiring
/// obligation: constructing a second Runtime in the same process yields a second
/// gate, which is why composition owns the single instance.
type CognitiveRuntime(port: CognitiveJournalPort) =

    let gates = Dictionary<string, Task<unit>>()

    // DSL-MUTABLE: projection cache — the last committed canvas per owner, keyed by
    // the physical session. Durable truth is the journal; this only accelerates the
    // owner's own reads, so another owner's commit can never appear here and a
    // restart recovers the same canvas from committed facts.
    let canvases = Dictionary<string, AssumeSnapshot>()

    let gateFor (ownerKey: string) : Task<unit> =
        match gates.TryGetValue ownerKey with
        | true, existing -> existing
        | false, _ ->
            let started = Task.FromResult(())
            gates.[ownerKey] <- started
            started

    let replaceGate (ownerKey: string) (previous: Task<unit>) (next: Task<unit>) =
        match gates.TryGetValue ownerKey with
        | true, current when Object.ReferenceEquals(current, previous) -> gates.[ownerKey] <- next
        | _ -> ()

    /// The owner's current canvas: this process's committed canvas when it has
    /// one, otherwise the canvas the owner's committed facts name.
    ///
    /// Fail-closed, not fail-silent: a projection that claims a snapshot whose blob
    /// is missing or unparsable is a refusal naming the owner and the reason.
    /// Answering an empty canvas there would silently discard a durable commit and
    /// let the next write overwrite history the next boot would still recover.
    member this.CurrentCanvas(owner: CognitiveOwner.T) : Task<Result<AssumeSnapshot, string>> =
        let ownerKey = CognitiveOwner.key owner

        match canvases.TryGetValue ownerKey with
        | true, snapshot -> Task.FromResult(Ok snapshot)
        | false, _ ->
            task {
                match port.ReadProjection ownerKey with
                | None -> return Ok AssumeSnapshot.empty
                | Some state ->
                    match state.SnapshotRef with
                    | None -> return Ok AssumeSnapshot.empty
                    | Some snapshotRef ->
                        match! port.ReadBlob snapshotRef with
                        | Error reason ->
                            return Error(sprintf "cannot recover the committed canvas for owner %s: %s" ownerKey reason)
                        | Ok text ->
                            match AssumeSnapshot.tryParseJson text with
                            | Error reason ->
                                return
                                    Error(
                                        sprintf "cannot recover the committed canvas for owner %s: %s" ownerKey reason
                                    )
                            | Ok snapshot ->
                                canvases.[ownerKey] <- snapshot
                                return Ok snapshot
            }

    /// Only a real commit changes this owner's canvas. A replay answers with the
    /// frozen first bytes and a rejection changes nothing, so neither case moves
    /// the cache: a rejected write must not become the next read's current canvas.
    member private this.ApplyOutcome
        (ownerKey: string)
        (outcome: CommitOutcome, canvasJson: string, todos: (string * TodoStatus * TodoPriority) list)
        =
        match outcome with
        | CommitOutcome.Committed _ -> canvases.[ownerKey] <- AssumeSnapshot.ofJson canvasJson todos
        | CommitOutcome.Replayed _
        | CommitOutcome.Rejected _ -> ()

    member _.Serialized<'result> (ownerKey: string) (work: unit -> Task<'result>) : Task<'result> =
        let previous = gateFor ownerKey
        let completion = TaskCompletionSource<'result>()

        let next =
            task {
                try
                    do! previous
                with _ ->
                    ()

                try
                    let! result = work ()
                    completion.SetResult result
                with error ->
                    completion.SetException error
            }

        replaceGate ownerKey previous next
        completion.Task

    /// Commit one phase for one owner: read the current canvas, run `transform`
    /// over it, then persist — the whole read-transform-write inside this owner's
    /// serial domain, so the next call's transform sees this call's committed canvas
    /// even when the Host dispatched both calls together.
    ///
    /// The committed canvas text travels beside the outcome so the tool can render
    /// the exact bytes that were persisted, not a re-encoding of them.
    member this.RunPhase
        (owner: CognitiveOwner.T)
        (toolCallId: ToolCallId)
        (inputDigest: string)
        (todos: (string * TodoStatus * TodoPriority) list)
        (transform: string -> Task<Result<string, string>>)
        : Task<CommitOutcome * string> =
        let ownerKey = CognitiveOwner.key owner

        this.Serialized ownerKey (fun () ->
            task {
                match! this.CurrentCanvas owner with
                | Error reason -> return CommitOutcome.Rejected reason, ""
                | Ok current ->
                    match! transform current.CanvasJson with
                    | Error reason -> return CommitOutcome.Rejected reason, ""
                    | Ok canvasJson ->
                        match port.ReadProjection ownerKey with
                        | Some state when state.LastToolCallId = Some toolCallId ->
                            return CommitInternals.replayOutcome state toolCallId inputDigest, canvasJson
                        | state ->
                            let! outcome =
                                CommitInternals.persistPhase
                                    port
                                    ownerKey
                                    owner
                                    toolCallId
                                    (CommitInternals.ordinalAfter state)
                                    inputDigest
                                    canvasJson
                                    todos

                            this.ApplyOutcome ownerKey (outcome, canvasJson, todos)
                            return outcome, canvasJson
            })
