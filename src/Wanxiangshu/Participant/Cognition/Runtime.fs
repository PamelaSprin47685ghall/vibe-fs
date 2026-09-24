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

/// Owner-scoped serial admission.
///
/// One queue per owner, so two owners never block each other, and a failed jq never
/// poisons the queue for the next legal call. Cross-instance sharing is a wiring
/// obligation: constructing a second Runtime in the same process yields a second
/// gate, which is why composition owns the single instance.
type CognitiveRuntime(port: CognitiveJournalPort) =

    let gates = Dictionary<string, Task<unit>>()

    // DSL-MUTABLE: resource — global monotonic ordinal generator for committed cognitive phases.
    let mutable latestOrdinal = 0L

    // DSL-MUTABLE: resource — local cognitive canvas memory holding current assume snapshot.
    let mutable canvas = AssumeSnapshot.empty

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

    member _.CurrentCanvas = canvas

    /// Only a real commit advances the ordinal. A replay already holds one and a
    /// rejection must not move it, so neither case touches the counter.
    member this.ApplyOutcome
        (outcome: CommitOutcome, canvasJson: string, todos: (string * TodoStatus * TodoPriority) list)
        =
        match outcome with
        | CommitOutcome.Committed ordinal ->
            latestOrdinal <- ordinal
            this.UpdateCanvas(AssumeSnapshot.ofJson canvasJson todos)
        | CommitOutcome.Replayed _
        | CommitOutcome.Rejected _ -> ()

    member this.AdvanceOrdinal(outcome: CommitOutcome) = this.ApplyOutcome(outcome, "{}", [])

    member _.UpdateCanvas(snapshot: AssumeSnapshot) = canvas <- snapshot

    member _.Serialized (ownerKey: string) (work: unit -> Task<CommitOutcome>) : Task<CommitOutcome> =
        let previous = gateFor ownerKey
        let completion = TaskCompletionSource<CommitOutcome>()

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

    /// Commit one phase. The caller has already validated the tool input shape and
    /// produced the new canvas plus normalised rows; this method owns ordering,
    /// persistence, idempotence and fold.
    member this.Commit
        (owner: CognitiveOwner.T)
        (toolCallId: ToolCallId)
        (inputDigest: string)
        (canvasJson: string)
        (todos: (string * TodoStatus * TodoPriority) list)
        : Task<CommitOutcome> =
        let ownerKey = CognitiveOwner.key owner

        this.Serialized ownerKey (fun () ->
            task {
                match port.ReadProjection ownerKey with
                | Some state when state.LastToolCallId = Some toolCallId ->
                    return CommitInternals.replayOutcome state toolCallId inputDigest
                | _ ->
                    let! outcome =
                        CommitInternals.persistPhase
                            port
                            ownerKey
                            owner
                            toolCallId
                            (latestOrdinal + 1L)
                            inputDigest
                            canvasJson
                            todos

                    this.ApplyOutcome(outcome, canvasJson, todos)
                    return outcome
            })
