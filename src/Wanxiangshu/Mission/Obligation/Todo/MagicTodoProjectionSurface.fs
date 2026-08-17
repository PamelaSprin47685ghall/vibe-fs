namespace Wanxiangshu.Mission.Obligation.Todo

open System
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoProjection

/// JS-native projection and typed-fact owner for Magic Todo.
/// The handle is a resource: F# maps, records, unions, and replay state never
/// cross the test boundary.
module private MagicTodoProjectionEncoding =

    let rejectionView (rejection: MagicTodoProjection.MagicTodoFoldRejection) : obj =
        match rejection with
        | MagicTodoFoldRejection.LifeMismatch(expected, actual) ->
            box
                {| code = "LifeMismatch"
                   expected = expected
                   actual = actual |}
        | MagicTodoFoldRejection.PreparedMissingForAccept writeId ->
            box
                {| code = "PreparedMissingForAccept"
                   todoWriteId = writeId |}
        | MagicTodoFoldRejection.OutstandingReviewBeforePrepare writeId ->
            box
                {| code = "OutstandingReviewBeforePrepare"
                   todoWriteId = writeId |}
        | MagicTodoFoldRejection.IdentityCorruption field ->
            box
                {| code = "IdentityCorruption"
                   field = field |}
        | MagicTodoFoldRejection.AssignmentWithoutAccepted writeId ->
            box
                {| code = "AssignmentWithoutAccepted"
                   todoWriteId = writeId |}
        | MagicTodoFoldRejection.ConcludedWithoutAccepted writeId ->
            box
                {| code = "ConcludedWithoutAccepted"
                   todoWriteId = writeId |}
        | MagicTodoFoldRejection.LegacySeedAfterCheckpoint -> box {| code = "LegacySeedAfterCheckpoint" |}
        | MagicTodoFoldRejection.DedicatedMissingForAssign -> box {| code = "DedicatedMissingForAssign" |}
        | MagicTodoFoldRejection.DedicatedMissingForReplace -> box {| code = "DedicatedMissingForReplace" |}

type MagicTodoProjectionHandle private (state: MagicTodoProjection.MagicTodoProjectionState) =
    // DSL-MUTABLE: resource — opaque projection handle current state
    let mutable current = state

    member _.Fold(eventId: string, factJson: string) : obj =
        match MagicTodoFactCodec.tryDecode factJson with
        | Error error ->
            box
                {| ok = false
                   error = box {| code = "Decode"; message = error |} |}
        | Ok fact ->
            match MagicTodoProjection.fold (EventId.create eventId) current fact with
            | Error rejection ->
                box
                    {| ok = false
                       error = MagicTodoProjectionEncoding.rejectionView rejection |}
            | Ok next ->
                current <- next
                box {| ok = true |}

    member _.State = current

    static member Create() =
        MagicTodoProjectionHandle(MagicTodoProjection.empty)

[<RequireQualifiedAccess>]
module MagicTodoProjectionSurface =

    let private optionString (value: string option) : obj =
        value |> Option.map (fun item -> box item) |> Option.toObj

    let private refView (reference: (BlobRef * BlobDigest) option) : obj =
        match reference with
        | None -> null
        | Some(reference, digest) ->
            box
                {| reference = BlobRef.value reference
                   digest = BlobDigest.value digest |}

    let private checkpointView (checkpoint: MagicTodoProjection.CheckpointRecord) : obj =
        let assignment =
            checkpoint.Assignment
            |> Option.map (fun value ->
                box
                    {| todoReviewId = TodoReviewId.value value.TodoReviewId
                       dedicatedReviewerId = DedicatedReviewerId.value value.DedicatedReviewerId
                       reviewerSessionId = SessionId.value value.ReviewerSessionId
                       reviewWorkStart = int value.ReviewWorkStartCursor.Sequence
                       managerReviewFrontier = int value.ManagerReviewFrontier.Sequence |})
            |> Option.toObj

        let concluded =
            checkpoint.Concluded
            |> Option.map (fun value ->
                box
                    {| todoReviewId = TodoReviewId.value value.TodoReviewId
                       dedicatedReviewerId = DedicatedReviewerId.value value.DedicatedReviewerId
                       reviewerSessionId = SessionId.value value.ReviewerSessionId
                       verdict = ProcessReviewVerdict.wire value.Verdict
                       workRecordRef = BlobRef.value value.WorkRecordRef
                       workRecordDigest = BlobDigest.value value.WorkRecordDigest |})
            |> Option.toObj

        box
            {| managerSessionId = SessionId.value checkpoint.ManagerSessionId
               todoWriteId = TodoWriteId.value checkpoint.TodoWriteId
               toolCallId = ToolCallId.value checkpoint.ToolCallId
               planCompleteDeclared = checkpoint.PlanCompleteDeclared
               providerInputDigest = checkpoint.ProviderInputDigest
               reviewFrontier = int checkpoint.ReviewFrontier.Sequence
               baseTodoRef = BlobRef.value checkpoint.BaseTodoRef
               baseTodoDigest = BlobDigest.value checkpoint.BaseTodoDigest
               proposedTodoRef = BlobRef.value checkpoint.ProposedTodoRef
               proposedTodoDigest = BlobDigest.value checkpoint.ProposedTodoDigest
               accepted = checkpoint.Accepted
               assignment = assignment
               concluded = concluded |}

    let internal rejectionView rejection : obj =
        MagicTodoProjectionEncoding.rejectionView rejection

    let internal lifeView (state: MagicTodoProjection.MagicTodoProjectionState) (lifeId: ManagerLifeId) : obj =
        match MagicTodoProjection.tryLife lifeId state with
        | None -> null
        | Some life ->
            let checkpoints =
                life.Checkpoints
                |> Map.toArray
                |> Array.map (fun (_, checkpoint) -> checkpointView checkpoint)

            box
                {| lifeId = ManagerLifeId.value life.LifeId
                   currentObligations = refView life.CurrentObligationsRef
                   firstAcceptedCheckpoint =
                    life.FirstAcceptedCheckpoint |> Option.map TodoWriteId.value |> optionString
                   latestAcceptedCheckpoint =
                    life.LatestAcceptedCheckpoint |> Option.map TodoWriteId.value |> optionString
                   pendingReviewCheckpoint =
                    life.PendingReviewCheckpoint |> Option.map TodoWriteId.value |> optionString
                   firstPlanCommitment = life.FirstPlanCommitment |> Option.map TodoWriteId.value |> optionString
                   latestCommittedCheckpoint =
                    life.LatestCommittedCheckpoint |> Option.map TodoWriteId.value |> optionString
                   previousCommittedCheckpoint =
                    life.PreviousCommittedCheckpoint |> Option.map TodoWriteId.value |> optionString
                   checkpoints = checkpoints
                   dedicated =
                    life.Dedicated
                    |> Option.map (fun value ->
                        box
                            {| dedicatedReviewerId = DedicatedReviewerId.value value.DedicatedReviewerId
                               reviewerSessionId = SessionId.value value.ReviewerSessionId |})
                    |> Option.toObj
                   legacySeed = refView life.LegacySeed |}

    let create () = MagicTodoProjectionHandle.Create()

    let fold (handle: MagicTodoProjectionHandle) (eventId: string) (factJson: string) : obj =
        handle.Fold(eventId, factJson)

    let view (handle: MagicTodoProjectionHandle) (lifeId: string) : obj =
        lifeView handle.State (ManagerLifeId.create lifeId)

    let reviewerLife (handle: MagicTodoProjectionHandle) (reviewerSessionId: string) : obj =
        handle.State.ReviewerLifeBySession
        |> Map.tryFind reviewerSessionId
        |> Option.map (fun value -> box (ManagerLifeId.value value))
        |> Option.toObj

    let state (handle: MagicTodoProjectionHandle) = handle.State
