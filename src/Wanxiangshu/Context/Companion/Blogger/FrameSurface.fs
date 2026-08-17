namespace Wanxiangshu.Context.Companion.Blogger

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity

/// Context-compression frame owner. The JavaScript boundary exchanges only
/// JSON-shaped frame/projection snapshots; BlogProjection's list, map, DU and
/// identity representations stay inside this module.
[<RequireQualifiedAccess>]
module BlogFrameSurface =

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private text (value: obj) : string =
        if isNullish value then "" else string value

    let private intValue (value: obj) : int =
        if isNullish value then 0 else int (text value)

    let private int64Value (value: obj) : int64 =
        if isNullish value then 0L else int64 (text value)

    let private frameOfJs (value: obj) : BlogFrame =
        let kind =
            match text value?kind with
            | "Squash" -> BlogFrameKind.Squash
            | _ -> BlogFrameKind.Entry

        { Kind = kind
          Digest = BlobDigest.create (text value?digest)
          TextRef = BlobRef.create (text value?ref)
          CoveredFromSequence = int64Value value?coveredFrom
          CoveredThroughSequence = int64Value value?coveredThrough }

    let private frameToJs (frame: BlogFrame) : obj =
        box
            {| kind =
                match frame.Kind with
                | BlogFrameKind.Entry -> "Entry"
                | BlogFrameKind.Squash -> "Squash"
               digest = BlobDigest.value frame.Digest
               ref = BlobRef.value frame.TextRef
               coveredFrom = int frame.CoveredFromSequence
               coveredThrough = int frame.CoveredThroughSequence |}

    let private stateToJs (state: BlogProjectionState) : obj =
        let coverage = state.Coverage

        box
            {| frameEpoch = int (FrameEpochId.value state.FrameEpochId)
               frames = BlogProjection.frames state |> List.map frameToJs |> List.toArray
               coverage =
                {| ingestedThroughSequence = int coverage.IngestedThroughSequence
                   cutoff = coverage.CoverableTurnCutoffExclusive
                   digest = coverage.CoveredPrefixDigest
                   coverableFrames = coverage.CoverableFrameCount |} |}

    let private stateOfJs (value: obj) : BlogProjectionState =
        let frames =
            (if isNullish value?frames then
                 [||]
             else
                 unbox<obj array> value?frames)
            |> Array.toList
            |> List.map frameOfJs
            |> List.rev

        let coverage = value?coverage

        { FrameEpochId = FrameEpochId.create (int64Value value?frameEpoch)
          Frames = frames
          Coverage =
            { IngestedThroughSequence = int64Value coverage?ingestedThroughSequence
              CoverableTurnCutoffExclusive = intValue coverage?cutoff
              CoveredPrefixDigest = text coverage?digest
              CoverableFrameCount = intValue coverage?coverableFrames } }

    let private rejectionName (rejection: BlogFoldRejection) : string =
        match rejection with
        | BlogFoldRejection.StaleFrameEpoch _ -> "StaleFrameEpoch"
        | BlogFoldRejection.NonSequentialFrameEpoch -> "NonSequentialFrameEpoch"
        | BlogFoldRejection.IngestCursorNotAdvanced -> "IngestCursorNotAdvanced"
        | BlogFoldRejection.IngestCursorMismatch -> "IngestCursorMismatch"
        | BlogFoldRejection.CoverageRetreated -> "CoverageRetreated"
        | BlogFoldRejection.CoveredFrameCountOutOfRange _ -> "CoveredFrameCountOutOfRange"

    let private resultToJs (ok: 'a -> obj) (result: Result<'a, BlogFoldRejection>) : obj =
        match result with
        | Ok value -> box {| ok = true; value = ok value |}
        | Error rejection ->
            box
                {| ok = false
                   error = rejectionName rejection |}

    /// Construct one frame from plain JSON data.
    let frame (value: obj) : obj = frameOfJs value |> frameToJs

    /// Empty durable frame projection.
    let empty: obj = stateToJs BlogProjection.empty

    /// Apply one atomic BlogObservationCommitted projection line. `request.frame`
    /// carries the frame and the remaining fields are the frozen commit proof.
    let applyEntry (request: obj) (state: obj) : obj =
        BlogProjection.applyEntry
            (FrameEpochId.create (int64Value request?epoch))
            (int64Value request?previous)
            (int64Value request?next)
            (intValue request?previousCutoff)
            (intValue request?nextCutoff)
            (text request?digest)
            (frameOfJs request?frame)
            (stateOfJs state)
        |> resultToJs stateToJs

    /// Apply one atomic BlogObservationsSquashed projection line.
    let applySquash (request: obj) (state: obj) : obj =
        BlogProjection.applySquash
            (FrameEpochId.create (int64Value request?previousEpoch))
            (FrameEpochId.create (int64Value request?nextEpoch))
            (intValue request?count)
            (frameOfJs request?frame)
            (stateOfJs state)
        |> resultToJs stateToJs

    /// Host compaction containment: retire PrefixCoverage while retaining frames
    /// and RecordCoverage.
    let applyReanchor (state: obj) : obj =
        stateOfJs state |> BlogProjection.applyReanchor |> stateToJs

    let frameCount (state: obj) : int =
        stateOfJs state |> BlogProjection.frameCount

    let frameEpochOf (state: obj) : int =
        stateOfJs state |> fun value -> int (FrameEpochId.value value.FrameEpochId)

    let frames (state: obj) : obj array =
        stateOfJs state |> BlogProjection.frames |> List.map frameToJs |> List.toArray

    let frameKinds (state: obj) : string array =
        stateOfJs state
        |> BlogProjection.frames
        |> List.map (fun frame ->
            match frame.Kind with
            | BlogFrameKind.Entry -> "Entry"
            | BlogFrameKind.Squash -> "Squash")
        |> List.toArray

    let coverableFrameKinds (state: obj) : string array =
        stateOfJs state
        |> BlogProjection.coverableFrames
        |> List.map (fun frame ->
            match frame.Kind with
            | BlogFrameKind.Entry -> "Entry"
            | BlogFrameKind.Squash -> "Squash")
        |> List.toArray

    let coverage (state: obj) : obj =
        let value = (stateOfJs state).Coverage

        box
            {| ingestedThroughSequence = int value.IngestedThroughSequence
               cutoff = value.CoverableTurnCutoffExclusive
               digest = value.CoveredPrefixDigest
               coverableFrames = value.CoverableFrameCount |}

    let hasCoverage (state: obj) : bool =
        stateOfJs state |> BlogProjection.hasCoverage

    let squashWidth (state: obj) : int =
        stateOfJs state |> BlogProjection.squashWidth
