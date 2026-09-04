namespace Wanxiangshu.Mission.Obligation.Todo

open System
open Fable.Core.JsInterop
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoProjection
open Wanxiangshu.Mission.Relay

/// JS-native projection and typed-fact owner for Magic Todo.
/// The handle is a resource: F# maps, records, unions, and replay state never
/// cross the test boundary.
module private MagicTodoProjectionEncoding =

    let rejectionView (rejection: MagicTodoProjection.MagicTodoFoldRejection) : obj =
        match rejection with
        | MagicTodoFoldRejection.IncumbencyMismatch(expected, actual) ->
            box
                {| code = "IncumbencyMismatch"
                   expected = expected
                   actual = actual |}
        | MagicTodoFoldRejection.PreparedMissingForAccept writeId ->
            box
                {| code = "PreparedMissingForAccept"
                   todoWriteId = writeId |}
        | MagicTodoFoldRejection.IdentityCorruption field ->
            box
                {| code = "IdentityCorruption"
                   field = field |}
        | MagicTodoFoldRejection.LegacySeedAfterCheckpoint -> box {| code = "LegacySeedAfterCheckpoint" |}

[<Sealed>]
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

    let private lifecycleView (lifecycle: MagicTodoProjection.CheckpointLifecycle) : obj =
        match lifecycle with
        | CheckpointLifecycle.Prepared -> box {| kind = "Prepared" |}
        | CheckpointLifecycle.Accepted accepted ->
            box
                {| kind = "Accepted"
                   inputDigest = accepted.InputDigest
                   outputDigest = accepted.OutputDigest |}

    let private checkpointView (checkpoint: MagicTodoProjection.CheckpointRecord) : obj =

        let inputDigest =
            MagicTodoProjection.acceptedEvidence checkpoint
            |> Option.map (fun e -> e.InputDigest)
            |> Option.toObj

        let outputDigest =
            MagicTodoProjection.acceptedEvidence checkpoint
            |> Option.map (fun e -> e.OutputDigest)
            |> Option.toObj

        box
            {| managerSessionId = SessionId.value checkpoint.ManagerSessionId
               todoWriteId = TodoWriteId.value checkpoint.TodoWriteId
               toolCallId = ToolCallId.value checkpoint.ToolCallId
               planCompleteDeclared = checkpoint.PlanCompleteDeclared
               providerInputDigest = checkpoint.ProviderInputDigest
               reviewFrontier = int (XTraceCursor.sequence checkpoint.ReviewFrontier)
               baseTodoRef = BlobRef.value checkpoint.BaseTodoRef
               baseTodoDigest = BlobDigest.value checkpoint.BaseTodoDigest
               proposedTodoRef = BlobRef.value checkpoint.ProposedTodoRef
               proposedTodoDigest = BlobDigest.value checkpoint.ProposedTodoDigest
               accepted = MagicTodoProjection.isAccepted checkpoint
               inputDigest = inputDigest
               outputDigest = outputDigest
               lifecycle = lifecycleView checkpoint.Lifecycle |}

    let internal rejectionView rejection : obj =
        MagicTodoProjectionEncoding.rejectionView rejection

    let internal incumbencyView
        (state: MagicTodoProjection.MagicTodoProjectionState)
        (incumbencyId: IncumbencyId)
        : obj =
        match MagicTodoProjection.tryIncumbency incumbencyId state with
        | None -> null
        | Some incumbency ->
            let checkpoints =
                incumbency.Checkpoints
                |> Map.toArray
                |> Array.map (fun (_, checkpoint) -> checkpointView checkpoint)

            box
                {| incumbencyId = IncumbencyId.value incumbency.IncumbencyId
                   currentObligations = refView incumbency.CurrentObligationsRef
                   firstAcceptedCheckpoint =
                    incumbency.FirstAcceptedCheckpoint |> Option.map TodoWriteId.value |> optionString
                   latestAcceptedCheckpoint =
                    incumbency.LatestAcceptedCheckpoint |> Option.map TodoWriteId.value |> optionString
                   firstPlanCommitment =
                    incumbency.FirstPlanCommitment |> Option.map TodoWriteId.value |> optionString
                   latestCommittedCheckpoint =
                    incumbency.LatestCommittedCheckpoint |> Option.map TodoWriteId.value |> optionString
                   previousCommittedCheckpoint =
                    incumbency.PreviousCommittedCheckpoint |> Option.map TodoWriteId.value |> optionString
                   checkpoints = checkpoints
                   legacySeed = refView incumbency.LegacySeed |}

    let create () = MagicTodoProjectionHandle.Create()

    let fold (handle: MagicTodoProjectionHandle) (eventId: string) (factJson: string) : obj =
        handle.Fold(eventId, factJson)

    let view (handle: MagicTodoProjectionHandle) (incumbencyId: string) : obj =
        incumbencyView handle.State (IncumbencyId.create incumbencyId)

    let state (handle: MagicTodoProjectionHandle) = handle.State
