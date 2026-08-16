namespace Wanxiangshu.Enforcer

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Enforcer.Cycle

/// JS-native owner boundary for paired tip/frame observations and their two
/// projection folds. The JSON state is a semantic snapshot, not an exposed F#
/// record: all identity wrappers, maps, lists and union cases stay private.
[<RequireQualifiedAccess>]
module ObservationSurface =

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private text (value: obj) : string =
        if isNullish value then "" else string value

    let private optionalText (value: obj) : string option =
        if isNullish value then None else Some(text value)

    let private arrayOf (value: obj) : obj array =
        if isNullish value then [||] else unbox<obj array> value

    let private optionalObj (value: 'a option) : obj =
        match value with
        | None -> null
        | Some item -> box item

    let private unitToJs (unit: ObservationUnit) : obj =
        box
            {| tipName = optionalObj unit.TipName
               frameDigest = optionalObj unit.FrameDigest
               frameBody = optionalObj unit.FrameBody |}

    let private workLogToJs (item: WorkLogObservation) : obj =
        box
            {| tipName = item.TipName
               cycleId = item.CycleId
               frameDigest = optionalObj item.FrameDigest |}

    let private frameOfJs (value: obj) : BlogFrame =
        let kind =
            match text value?kind with
            | "Squash" -> BlogFrameKind.Squash
            | _ -> BlogFrameKind.Entry

        { Kind = kind
          Digest = BlobDigest.create (text value?digest)
          TextRef = BlobRef.create (text value?ref)
          CoveredFromSequence = int64 (text value?coveredFrom)
          CoveredThroughSequence = int64 (text value?coveredThrough) }

    let private frameToJs (frame: BlogFrame) : obj =
        box
            {| kind =
                match frame.Kind with
                | BlogFrameKind.Entry -> "Entry"
                | BlogFrameKind.Squash -> "Squash"
               digest = BlobDigest.value frame.Digest
               ref = BlobRef.value frame.TextRef
               coveredFrom = frame.CoveredFromSequence
               coveredThrough = frame.CoveredThroughSequence |}

    let private blogStateToJs (state: BlogProjectionState) : obj =
        let coverage = state.Coverage

        box
            {| frameEpoch = int (FrameEpochId.value state.FrameEpochId)
               frames = BlogProjection.frames state |> List.map frameToJs |> List.toArray
               coverage =
                {| ingestedThroughSequence = int coverage.IngestedThroughSequence
                   coverableTurnCutoffExclusive = coverage.CoverableTurnCutoffExclusive
                   coveredPrefixDigest = coverage.CoveredPrefixDigest
                   coverableFrameCount = coverage.CoverableFrameCount |} |}

    let private blogStateOfJs (value: obj) : BlogProjectionState =
        let frames =
            arrayOf value?frames
            |> Array.toList
            |> List.map frameOfJs
            |> List.rev

        let coverage = value?coverage

        { FrameEpochId = FrameEpochId.create (int64 (text value?frameEpoch))
          Frames = frames
          Coverage =
            { IngestedThroughSequence = int64 (text coverage?ingestedThroughSequence)
              CoverableTurnCutoffExclusive = int (text coverage?coverableTurnCutoffExclusive)
              CoveredPrefixDigest = text coverage?coveredPrefixDigest
              CoverableFrameCount = int (text coverage?coverableFrameCount) } }

    let private tipToJs (tip: RecentTip) : obj =
        box
            {| ruleId = tip.RuleId
               fieldName = tip.FieldName
               cycleId = tip.CycleId |}

    let private cycleToJs (record: EnforcementCycleRecord) : obj =
        box
            {| mainSessionId = SessionId.value record.MainSessionId
               bloggerSessionId = SessionId.value record.BloggerSessionId
               run = ProviderRunIdentity.value record.ProviderRun
               toolCallIds = record.ToolCallIds |> List.map ToolCallId.value |> List.toArray
               textRef = BlobRef.value record.CycleTextRef
               textDigest = BlobDigest.value record.CycleTextDigest
               tipRuleId = record.TipRuleId
               fieldNameAtCommit = optionalObj record.FieldNameAtCommit
               evidenceRef = record.CycleEvidenceRef |> Option.map BlobRef.value |> optionalObj
               observedPrefixEpoch = int (PrefixEpochId.value record.ObservedPrefixEpochId) |}

    let private stateToJs (state: EnforcementProjectionState) : obj =
        box
            {| records =
                state.ByProviderRun
                |> Map.toList
                |> List.map (fun (_, record) -> cycleToJs record)
                |> List.toArray
               recentTips = state.RecentTips |> List.map tipToJs |> List.toArray |}

    let private stateOfJs (value: obj) : EnforcementProjectionState =
        let records =
            arrayOf value?records
            |> Array.toList
            |> List.map (fun item ->
                let run = ProviderRunIdentity.create (text item?run)
                run,
                { MainSessionId = SessionId.create (text item?mainSessionId)
                  BloggerSessionId = SessionId.create (text item?bloggerSessionId)
                  ProviderRun = run
                  ToolCallIds =
                    arrayOf item?toolCallIds
                    |> Array.toList
                    |> List.map (fun callId -> ToolCallId.create (text callId))
                  CycleTextRef = BlobRef.create (text item?textRef)
                  CycleTextDigest = BlobDigest.create (text item?textDigest)
                  TipRuleId = text item?tipRuleId
                  FieldNameAtCommit = optionalText item?fieldNameAtCommit
                  CycleEvidenceRef = optionalText item?evidenceRef |> Option.map BlobRef.create
                  ObservedPrefixEpochId = PrefixEpochId.create (int64 (text item?observedPrefixEpoch)) })
            |> Map.ofList

        let tips =
            arrayOf value?recentTips
            |> Array.toList
            |> List.map (fun item ->
                { RuleId = text item?ruleId
                  FieldName = text item?fieldName
                  CycleId = text item?cycleId })

        { ByProviderRun = records; RecentTips = tips }

    let private resultToJs (ok: 'a -> obj) (error: 'e -> obj) (result: Result<'a, 'e>) : obj =
        match result with
        | Ok value -> box {| ok = true; value = ok value |}
        | Error reason -> box {| ok = false; error = error reason |}

    let private errorText (error: 'e) = box (string error)

    let private cycleRecordOfJs (value: obj) : EnforcementCycleRecord =
        { MainSessionId = SessionId.create (text value?mainSessionId)
          BloggerSessionId = SessionId.create (text value?bloggerSessionId)
          ProviderRun = ProviderRunIdentity.create (text value?run)
          ToolCallIds =
            arrayOf value?toolCallIds
            |> Array.toList
            |> List.map (fun callId -> ToolCallId.create (text callId))
          CycleTextRef = BlobRef.create (text value?textRef)
          CycleTextDigest = BlobDigest.create (text value?textDigest)
          TipRuleId = text value?tipRuleId
          FieldNameAtCommit = optionalText value?fieldNameAtCommit
          CycleEvidenceRef = optionalText value?evidenceRef |> Option.map BlobRef.create
          ObservedPrefixEpochId = PrefixEpochId.create (int64 (text value?observedPrefixEpoch)) }

    let private observationOfJs (value: obj) : WorkLogObservation =
        { TipName = text value?tipName
          CycleId = text value?cycleId
          FrameDigest = optionalText value?frameDigest }

    /// Front-zip tips and frame `{ digest, body }` values. Remaining tips and
    /// frames stay explicitly unpaired in the returned observation units.
    let pairTipsAndFrames (tips: string array) (frames: obj array) : obj array =
        let frameValues =
            frames
            |> Array.toList
            |> List.map (fun frame -> text frame?digest, optionalText frame?body)

        RulebookObservation.pairTipsAndFrames (Array.toList tips) frameValues
        |> List.map unitToJs
        |> List.toArray

    /// Tip-anchored front zip. Frames without a tip are deliberately dropped.
    let ofTipsAndFrames (tips: obj array) (frameDigests: string array) : obj array =
        let tipValues =
            tips
            |> Array.toList
            |> List.map (fun item -> text item?tipName, text item?cycleId)

        RulebookObservation.ofTipsAndFrames tipValues (Array.toList frameDigests)
        |> List.map workLogToJs
        |> List.toArray

    let workLogFromUnits (tips: obj array) (units: obj array) : obj array =
        let tipValues =
            tips
            |> Array.toList
            |> List.map (fun item -> text item?tipName, text item?cycleId)

        let unitValues = units |> Array.toList |> List.map (fun item ->
            { TipName = optionalText item?tipName
              FrameDigest = optionalText item?frameDigest
              FrameBody = optionalText item?frameBody })

        RulebookObservation.workLogFromUnits tipValues unitValues
        |> List.map workLogToJs
        |> List.toArray

    let emptyEnforcement : obj = stateToJs EnforcementProjection.empty

    let applyEnforcementCycle (state: obj) (cycle: obj) : obj =
        EnforcementProjection.applyFromEntry (stateOfJs state) (cycleRecordOfJs cycle)
        |> resultToJs stateToJs errorText

    let applyEnforcementSquash (count: int) (state: obj) : obj =
        EnforcementProjection.applySquash count (stateOfJs state) |> stateToJs

    let recentTips (state: obj) : obj array =
        stateOfJs state |> EnforcementProjection.recentTips |> List.map tipToJs |> List.toArray

    let enforcementRecordCount (state: obj) : int = stateOfJs state |> fun value -> value.ByProviderRun.Count

    let emptyBlog : obj = blogStateToJs BlogProjection.empty

    let blogFrame (value: obj) : obj =
        value |> frameOfJs |> frameToJs

    let applyBlogEntry (request: obj) (frame: obj) (state: obj) : obj =
        let input = blogStateOfJs state
        BlogProjection.applyEntry
            (FrameEpochId.create (int64 (text request?frameEpoch)))
            (int64 (text request?previousIngestedThroughSequence))
            (int64 (text request?nextIngestedThroughSequence))
            (int (text request?previousCoverableTurnCutoffExclusive))
            (int (text request?nextCoverableTurnCutoffExclusive))
            (text request?nextCoveredPrefixDigest)
            (frameOfJs frame)
            input
        |> resultToJs blogStateToJs errorText

    let applyBlogSquash (request: obj) (frame: obj) (state: obj) : obj =
        BlogProjection.applySquash
            (FrameEpochId.create (int64 (text request?previousFrameEpoch)))
            (FrameEpochId.create (int64 (text request?nextFrameEpoch)))
            (int (text request?coveredFrameCount))
            (frameOfJs frame)
            (blogStateOfJs state)
        |> resultToJs blogStateToJs errorText

    let frameCount (state: obj) : int = blogStateOfJs state |> BlogProjection.frameCount

    let frameKinds (state: obj) : string array =
        BlogProjection.frames (blogStateOfJs state)
        |> List.map (fun frame -> match frame.Kind with | BlogFrameKind.Entry -> "Entry" | BlogFrameKind.Squash -> "Squash")
        |> List.toArray

    let coverage (state: obj) : obj =
        let value = (blogStateOfJs state).Coverage
        box
            {| ingestedThroughSequence = int value.IngestedThroughSequence
               coverableTurnCutoffExclusive = value.CoverableTurnCutoffExclusive
               coveredPrefixDigest = value.CoveredPrefixDigest
               coverableFrameCount = value.CoverableFrameCount |}

    let observationsOf (enforcement: obj) (blog: obj) : obj array =
        let enforcementState = if isNullish enforcement then None else Some(stateOfJs enforcement)
        let blogState = if isNullish blog then None else Some(blogStateOfJs blog)

        ObservationProjection.observationsOf enforcementState blogState
        |> List.map workLogToJs
        |> List.toArray

    let observationsAfterSquash (count: int) (enforcement: obj) (blog: obj) : obj array =
        ObservationProjection.observationsAfterSquash count (stateOfJs enforcement) (blogStateOfJs blog)
        |> List.map workLogToJs
        |> List.toArray
