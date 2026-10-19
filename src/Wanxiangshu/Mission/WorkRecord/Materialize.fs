namespace Wanxiangshu.Mission.WorkRecord

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Persistence.Journal

/// COMPANION-003 / EXEC-006 / EXEC-008: session LifecycleWorkRecord as opaque text.
///
/// Opening must have been captured even when `includeOpening` is false. Trace
/// selection and rendering remain owned by XTrace; this projection only combines
/// those rendered facts with verified Chronicle frames.
module LifecycleWorkRecordProjection =

    let private tryResolveFrameText (durable: AgentJournal) (frame: BlogFrame) : Task<string option> =
        task {
            match! durable.Writer.BlobWriter.Read frame.TextRef with
            | Ok text when HostDigest.sha256Hex text = BlobDigest.value frame.Digest -> return Some text
            | _ -> return None
        }

    let private resolveFrames (durable: AgentJournal) (frames: BlogFrame list) : Task<Result<string list, string>> =
        frames
        |> TaskResultList.traverseM (fun frame ->
            task {
                let! text = tryResolveFrameText durable frame

                return
                    text
                    |> Result.requireSome ("WorkRecord frame unavailable or corrupt: " + BlobDigest.value frame.Digest)
            })

    let private tryReadPartBody xTrace (part: XTraceSemanticPartView) : Task<string option> =
        XTraceProjection.partText part.Provenance xTrace
        |> Result.toOption
        |> Task.FromResult

    let private readVerifiedTerminalText
        (xTrace: XTraceProjectionState)
        (terminal: XTraceTerminalEvidence)
        : Task<string option> =
        XTraceProjection.terminalText terminal.ProviderRun xTrace
        |> Result.toOption
        |> Option.filter (String.IsNullOrWhiteSpace >> not)
        |> Task.FromResult

    let private isNaturalAssistant (part: XTraceSemanticPartView) =
        part.Role = "assistant" && (part.Kind = "text" || part.Kind = "reasoning")

    let private readOptionalPartBody
        (xTrace: XTraceProjectionState)
        (part: XTraceSemanticPartView)
        : Task<Result<string option, string>> =
        task {
            let! body = tryReadPartBody xTrace part
            return Ok body
        }

    let private readPartBodies xTrace parts =
        task {
            let! bodies = parts |> TaskResultList.traverseM (readOptionalPartBody xTrace)
            return bodies |> Result.defaultValue [] |> List.choose id
        }

    let private latestNaturalTurn (ordered: XTraceSemanticPartView list) =
        match ordered |> List.filter isNaturalAssistant |> List.tryLast with
        | None -> []
        | Some latest ->
            ordered
            |> List.filter (fun part ->
                isNaturalAssistant part
                && part.Turn = latest.Turn
                && part.Generation = latest.Generation)

    let private terminalAlreadyCaptured xTrace ordered terminalText : Task<bool> =
        task {
            match latestNaturalTurn ordered with
            | [] -> return false
            | relevant ->
                let! bodies = readPartBodies xTrace relevant
                return String.concat "\n\n" bodies = terminalText
        }

    /// Evidence → Decision: terminal bytes already present in the natural trace.
    let private appendTerminalWhenMissing rendered terminalText alreadyCaptured =
        let fallback = "assistant: " + terminalText

        match alreadyCaptured, String.IsNullOrEmpty rendered with
        | true, _ -> rendered
        | false, true -> fallback
        | false, false -> rendered + "\n" + fallback

    let private appendTerminalText xTrace ordered rendered terminalText =
        task {
            let! alreadyCaptured = terminalAlreadyCaptured xTrace ordered terminalText
            return appendTerminalWhenMissing rendered terminalText alreadyCaptured
        }

    let private appendTerminalFallback
        (xTrace: XTraceProjectionState)
        (ordered: XTraceSemanticPartView list)
        (rendered: string)
        (terminal: XTraceTerminalEvidence)
        : Task<string> =
        task {
            let! terminalText = readVerifiedTerminalText xTrace terminal

            match terminalText with
            | None -> return rendered
            | Some text -> return! appendTerminalText xTrace ordered rendered text
        }

    let private laterCursor left right =
        if XTraceCursor.isAfter left right then left else right

    let private coverageOrDefault (coverageOverride: RecordCoverage option) (blog: BlogProjectionState) =
        coverageOverride
        |> Option.defaultWith (fun () ->
            blog.Coverage.IngestedThroughSequence
            |> XTraceCursor.create
            |> RecordCoverage.create)

    /// work-record-008/009: the Opening interval is the trace's own leading
    /// material, [record start, OpeningBoundary). It begins at the first
    /// semantic part's cursor and ends at that part's frontier, so
    /// `XTraceRange.endExclusive` is WorkRecordStart (work-record-015) and the
    /// part itself stays inside the Opening instead of being consumed as work.
    /// Rendering stays owned by XTrace (`renderRange`): no second authority, and
    /// raw tool call/result is preserved here — Chronicle/Recent work filter it.
    /// An empty trace has an empty interval, never a synthesized one.
    let private openingInterval (xTrace: XTraceProjectionState) : XTraceRange =
        match xTrace |> XTraceProjection.orderedSemanticParts |> List.tryHead with
        | Some first -> XTraceRange.create first.Cursor (XTraceProjection.frontierAfter first)
        | None -> XTraceRange.create XTraceCursor.originCursor XTraceCursor.originCursor

    type private RenderedRecordEvidence =
        { ConstitutiveBody: string
          Gap: string }

    /// Evidence → Decision: both owner render operations proved their blobs.
    let private renderedRecordEvidence constitutive gap =
        match constitutive, gap with
        | Ok constitutiveBody, Ok renderedGap ->
            Some
                { ConstitutiveBody = constitutiveBody
                  Gap = renderedGap }
        | _ -> None

    let private completeFullGap xTrace gapStart renderedGap =
        task {
            match XTraceProjection.latestTerminalEvidence xTrace with
            | Some terminal when XTraceCursor.isAtOrAfter terminal.Frontier gapStart ->
                return!
                    appendTerminalFallback xTrace (XTraceProjection.orderedSemanticParts xTrace) renderedGap terminal
            | _ -> return renderedGap
        }

    let private materializeOpeningEvidence durable session includeOpening coverageOverride xTrace opening =
        task {
            let blog = session.Blog |> Option.defaultValue BlogProjection.empty
            let! frames = BlogProjection.frames blog |> resolveFrames durable
            let constitutiveRange = openingInterval xTrace
            let openingEnd = XTraceRange.endExclusive constitutiveRange
            let coverage = coverageOrDefault coverageOverride blog
            let gapStart = laterCursor (RecordCoverage.ingestedThrough coverage) openingEnd
            let gapRange = XTraceProjection.rangeFrom gapStart xTrace

            let! constitutive = XTraceMaterialization.renderRange constitutiveRange xTrace
            let! gap = XTraceMaterialization.renderWorkRecordRange gapRange xTrace

            match frames, renderedRecordEvidence constitutive gap with
            | Error _, _
            | _, None -> return None
            | Ok verifiedFrames, Some evidence ->
                let openingMaterial =
                    LifecycleWorkRecord.withConstitutive opening evidence.ConstitutiveBody

                let! finalGap = completeFullGap xTrace gapStart evidence.Gap
                return Some(LifecycleWorkRecord.materialize openingMaterial verifiedFrames finalGap includeOpening)
        }

    let private materializeOpenedSession
        (durable: AgentJournal)
        (snapshot: ProjectionSet)
        (session: SessionAgentProjection)
        (includeOpening: bool)
        (coverageOverride: RecordCoverage option)
        : Task<string option> =
        task {
            let xTrace = session.XTrace |> Option.defaultValue XTraceProjection.empty

            match XTraceProjection.openingEvidence xTrace with
            | None -> return None
            | Some opening ->
                return! materializeOpeningEvidence durable session includeOpening coverageOverride xTrace opening
        }

    let lifecycleWorkRecordFromSnapshot
        (durable: AgentJournal)
        (snapshot: ProjectionSet)
        (sessionId: SessionId)
        (includeOpening: bool)
        (coverageOverride: RecordCoverage option)
        : Task<string option> =
        task {
            match AgentProjection.tryFind sessionId snapshot.AgentProjections with
            | None -> return None
            | Some session -> return! materializeOpenedSession durable snapshot session includeOpening coverageOverride
        }

    let lifecycleWorkRecord
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (includeOpening: bool)
        : Task<string option> =
        match journal with
        | None -> Task.FromResult None
        | Some durable ->
            lifecycleWorkRecordFromSnapshot durable (AgentJournal.snapshot durable) sessionId includeOpening None

    let private framePrefixInRange (range: XTraceRange) (frames: BlogFrame list) =
        let startSequence = range |> XTraceRange.startInclusive |> XTraceCursor.sequence
        let endSequence = range |> XTraceRange.endExclusive |> XTraceCursor.sequence

        let rec takePrefix covered remaining selected =
            match remaining with
            | frame :: rest when
                frame.CoveredFromSequence = covered
                && frame.CoveredThroughSequence < endSequence
                ->
                takePrefix frame.CoveredThroughSequence rest (frame :: selected)
            | _ -> List.rev selected

        frames
        |> List.skipWhile (fun frame -> frame.CoveredThroughSequence < startSequence)
        |> fun remaining -> takePrefix (max 0L (startSequence - 1L)) remaining []

    let private boundedWorkRange
        (providerRun: ProviderRunIdentity option)
        (range: XTraceRange)
        (xTrace: XTraceProjectionState)
        =
        match providerRun with
        | None -> range
        | Some _ ->
            let workStart =
                XTraceProjection.slice range xTrace
                |> List.tryFind (fun part -> part.Role = "assistant")
                |> Option.map (fun part -> part.Cursor)
                |> Option.defaultValue (XTraceRange.endExclusive range)

            XTraceRange.create workStart (XTraceRange.endExclusive range)

    let private sameCursor left right =
        XTraceCursor.sequence left = XTraceCursor.sequence right

    let private terminalForBoundedRange
        (providerRun: ProviderRunIdentity option)
        (range: XTraceRange)
        (xTrace: XTraceProjectionState)
        =
        providerRun
        |> Option.bind (fun run -> XTraceProjection.terminalEvidenceForProviderRun run xTrace)
        |> Option.filter (fun terminal -> sameCursor terminal.Frontier (XTraceRange.endExclusive range))

    let private boundedGapStart frames workRange xTrace =
        let covered =
            frames
            |> List.tryLast
            |> Option.map (fun frame -> XTraceCursor.create frame.CoveredThroughSequence)
            |> Option.defaultValue (XTraceRange.startInclusive workRange)

        XTraceProjection.slice workRange xTrace
        |> List.filter (fun part -> part.Role = "assistant" && part.Kind = "text")
        |> List.tryLast
        |> Option.filter (fun part -> XTraceCursor.isBefore part.Cursor covered)
        |> Option.map (fun part -> part.Cursor)
        |> Option.defaultValue covered

    let private terminalCapturedWithVisibleSuffix xTrace workRange gapRange terminalText =
        task {
            let terminalParts = XTraceProjection.slice workRange xTrace |> latestNaturalTurn

            let! renderedBodies =
                terminalParts
                |> List.filter (fun part -> XTraceRange.contains part.Cursor gapRange)
                |> readPartBodies xTrace

            if renderedBodies |> List.exists (String.IsNullOrWhiteSpace >> not) then
                return! terminalAlreadyCaptured xTrace terminalParts terminalText
            else
                return false
        }

    let private terminalFormalAlreadyCaptured xTrace ordered terminalText =
        task {
            match
                latestNaturalTurn ordered
                |> List.filter (fun part -> part.Kind = "text")
                |> List.tryLast
            with
            | None -> return false
            | Some formal ->
                let! body = tryReadPartBody xTrace formal
                return body = Some terminalText
        }

    let private boundedTerminalAlreadyCaptured xTrace workRange gapRange terminalText =
        task {
            let gapParts = XTraceProjection.slice gapRange xTrace
            let! capturedInGap = terminalAlreadyCaptured xTrace gapParts terminalText
            let! capturedFormal = terminalFormalAlreadyCaptured xTrace gapParts terminalText

            if capturedInGap || capturedFormal then
                return true
            else
                return! terminalCapturedWithVisibleSuffix xTrace workRange gapRange terminalText
        }

    let private completeBoundedGap xTrace workRange gapRange range terminalProviderRun renderedGap =
        task {
            let! terminalText =
                terminalForBoundedRange terminalProviderRun range xTrace
                |> Option.map (readVerifiedTerminalText xTrace)
                |> Option.defaultValue (Task.FromResult None)

            match terminalText with
            | None -> return renderedGap
            | Some text ->
                let! alreadyCaptured = boundedTerminalAlreadyCaptured xTrace workRange gapRange text
                return appendTerminalWhenMissing renderedGap text alreadyCaptured
        }

    let private materializeBoundedFrames durable selectedFrames workRange range terminalProviderRun xTrace opening =
        task {
            let! frames = resolveFrames durable selectedFrames

            let gapRange =
                XTraceRange.create
                    (boundedGapStart selectedFrames workRange xTrace)
                    (XTraceRange.endExclusive workRange)

            let! gap = XTraceMaterialization.renderWorkRecordRange gapRange xTrace

            match frames, gap with
            | Error _, _
            | _, Error _ -> return None
            | Ok verifiedFrames, Ok renderedGap ->
                let! finalGap = completeBoundedGap xTrace workRange gapRange range terminalProviderRun renderedGap
                return Some(LifecycleWorkRecord.materialize opening verifiedFrames finalGap false)
        }

    let private materializeBoundedOpeningEvidence durable session range terminalProviderRun xTrace opening =
        task {
            let blog = session.Blog |> Option.defaultValue BlogProjection.empty
            let workRange = boundedWorkRange terminalProviderRun range xTrace

            let selection =
                session.CompletedWorkRecords
                |> Option.map (
                    CompletedWorkRecordProjection.framesForRange terminalProviderRun (XTraceRange.endExclusive range)
                )
                |> Option.defaultValue (Ok None)
                |> Result.map (fun frames ->
                    frames
                    |> Option.defaultWith (fun () -> BlogProjection.frames blog)
                    |> framePrefixInRange workRange)

            match selection with
            | Error _ -> return None
            | Ok selectedFrames ->
                return!
                    materializeBoundedFrames durable selectedFrames workRange range terminalProviderRun xTrace opening
        }

    let private materializeBoundedOpenedSession
        (durable: AgentJournal)
        (session: SessionAgentProjection)
        (range: XTraceRange)
        (terminalProviderRun: ProviderRunIdentity option)
        : Task<string option> =
        task {
            let xTrace = session.XTrace |> Option.defaultValue XTraceProjection.empty

            match XTraceProjection.openingEvidence xTrace with
            | None -> return None
            | Some opening ->
                return! materializeBoundedOpeningEvidence durable session range terminalProviderRun xTrace opening
        }

    let lifecycleWorkRecordBoundedFromSnapshot
        (durable: AgentJournal)
        (snapshot: ProjectionSet)
        (sessionId: SessionId)
        (range: XTraceRange)
        : Task<string option> =
        task {
            match AgentProjection.tryFind sessionId snapshot.AgentProjections with
            | None -> return None
            | Some session -> return! materializeBoundedOpenedSession durable session range None
        }

    let lifecycleWorkRecordBoundedFromSnapshotForRun
        (durable: AgentJournal)
        (snapshot: ProjectionSet)
        (sessionId: SessionId)
        (range: XTraceRange)
        (providerRun: ProviderRunIdentity)
        : Task<string option> =
        task {
            match AgentProjection.tryFind sessionId snapshot.AgentProjections with
            | None -> return None
            | Some session -> return! materializeBoundedOpenedSession durable session range (Some providerRun)
        }

    let lifecycleWorkRecordBounded
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (range: XTraceRange)
        : Task<string option> =
        match journal with
        | None -> Task.FromResult None
        | Some durable -> lifecycleWorkRecordBoundedFromSnapshot durable (AgentJournal.snapshot durable) sessionId range
