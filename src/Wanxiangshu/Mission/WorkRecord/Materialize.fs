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
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
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

    let private resolveFrames (durable: AgentJournal) (frames: BlogFrame list) : Task<string list> =
        task {
            let resolved = ResizeArray<string>()

            for frame in frames do
                let! text = tryResolveFrameText durable frame
                Option.iter resolved.Add text

            return resolved |> Seq.toList
        }

    let private tryReadPartBody (durable: AgentJournal) (part: XTraceSemanticPartView) : Task<string option> =
        task {
            match! durable.Writer.BlobWriter.Read part.TextRef with
            | Ok body when HostDigest.sha256Hex body = BlobDigest.value part.TextDigest -> return Some body
            | _ -> return None
        }

    let private readVerifiedTerminalText
        (durable: AgentJournal)
        (terminal: XTraceTerminalEvidence)
        : Task<string option> =
        task {
            match! durable.Writer.BlobWriter.Read terminal.TextRef with
            | Ok text when
                HostDigest.sha256Hex text = BlobDigest.value terminal.TextDigest
                && not (String.IsNullOrWhiteSpace text)
                ->
                return Some text
            | _ -> return None
        }

    let private isNaturalAssistant (part: XTraceSemanticPartView) =
        part.Role = "assistant" && (part.Kind = "text" || part.Kind = "reasoning")

    let private readOptionalPartBody
        (durable: AgentJournal)
        (part: XTraceSemanticPartView)
        : Task<Result<string option, string>> =
        task {
            let! body = tryReadPartBody durable part
            return Ok body
        }

    let private readPartBodies durable parts =
        task {
            let! bodies = parts |> TaskResultList.traverseM (readOptionalPartBody durable)
            return bodies |> Result.defaultValue [] |> List.choose id
        }

    let private terminalAlreadyCaptured
        (durable: AgentJournal)
        (ordered: XTraceSemanticPartView list)
        (terminalText: string)
        : Task<bool> =
        task {
            match ordered |> List.filter isNaturalAssistant |> List.tryLast with
            | None -> return false
            | Some latest ->
                let relevant =
                    ordered
                    |> List.filter (fun part ->
                        part.Role = "assistant"
                        && part.Turn = latest.Turn
                        && part.Generation = latest.Generation
                        && (part.Kind = "text" || part.Kind = "reasoning"))

                let! bodies = readPartBodies durable relevant
                return String.concat "\n\n" bodies = terminalText
        }

    /// Evidence → Decision: terminal bytes already present in the natural trace.
    let private appendTerminalWhenMissing rendered terminalText alreadyCaptured =
        let fallback = "assistant: " + terminalText

        match alreadyCaptured, String.IsNullOrEmpty rendered with
        | true, _ -> rendered
        | false, true -> fallback
        | false, false -> rendered + "\n" + fallback

    let private appendTerminalText durable ordered rendered terminalText =
        task {
            let! alreadyCaptured = terminalAlreadyCaptured durable ordered terminalText
            return appendTerminalWhenMissing rendered terminalText alreadyCaptured
        }

    let private appendTerminalFallback
        (durable: AgentJournal)
        (ordered: XTraceSemanticPartView list)
        (rendered: string)
        (terminal: XTraceTerminalEvidence)
        : Task<string> =
        task {
            let! terminalText = readVerifiedTerminalText durable terminal

            match terminalText with
            | None -> return rendered
            | Some text -> return! appendTerminalText durable ordered rendered text
        }

    let private laterCursor left right =
        if XTraceCursor.isAfter left right then left else right

    let private coverageOrDefault (coverageOverride: RecordCoverage option) (blog: BlogProjectionState) =
        coverageOverride
        |> Option.defaultWith (fun () ->
            blog.Coverage.IngestedThroughSequence
            |> XTraceCursor.create
            |> RecordCoverage.create)

    let private immediateOpeningEnd (xTrace: XTraceProjectionState) =
        xTrace
        |> XTraceProjection.orderedSemanticParts
        |> List.tryHead
        |> Option.map XTraceProjection.frontierAfter
        |> Option.defaultValue XTraceCursor.originCursor

    let private resolveOpeningEnd
        (life: LifeProjection option)
        (snapshot: ProjectionSet)
        (xTrace: XTraceProjectionState)
        =
        life
        |> Option.bind (fun current ->
            ManagerOpeningFloor.workRecordStart
                current
                (MagicTodoProjection.tryLife current.LifeId snapshot.AgentProjections.MagicTodo)
                xTrace)
        |> Option.defaultWith (fun () -> immediateOpeningEnd xTrace)

    let private renderRange (durable: AgentJournal) (range: XTraceRange) (xTrace: XTraceProjectionState) =
        XTraceMaterialization.renderRange durable range xTrace

    let private renderWorkRecordRange (durable: AgentJournal) (range: XTraceRange) (xTrace: XTraceProjectionState) =
        XTraceMaterialization.renderWorkRecordRange durable range xTrace

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

    let private completeFullGap durable xTrace gapStart renderedGap =
        task {
            match XTraceProjection.latestTerminalEvidence xTrace with
            | Some terminal when XTraceCursor.isAtOrAfter terminal.Frontier gapStart ->
                return!
                    appendTerminalFallback durable (XTraceProjection.orderedSemanticParts xTrace) renderedGap terminal
            | _ -> return renderedGap
        }

    let private materializeOpeningEvidence durable snapshot session includeOpening coverageOverride xTrace opening =
        task {
            let blog = session.Blog |> Option.defaultValue BlogProjection.empty
            let! frames = BlogProjection.frames blog |> resolveFrames durable

            let life =
                session.ManagerLife |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)

            let openingEnd = resolveOpeningEnd life snapshot xTrace

            let constitutiveStart =
                xTrace
                |> XTraceProjection.orderedSemanticParts
                |> List.tryHead
                |> Option.map XTraceProjection.frontierAfter
                |> Option.defaultValue openingEnd

            let constitutiveRange = XTraceRange.create constitutiveStart openingEnd
            let coverage = coverageOrDefault coverageOverride blog
            let gapStart = laterCursor (RecordCoverage.ingestedThrough coverage) openingEnd
            let gapRange = XTraceProjection.rangeFrom gapStart xTrace

            let! constitutive = renderRange durable constitutiveRange xTrace
            let! gap = renderWorkRecordRange durable gapRange xTrace

            match renderedRecordEvidence constitutive gap with
            | None -> return None
            | Some evidence ->
                let openingMaterial =
                    LifecycleWorkRecord.withConstitutive opening evidence.ConstitutiveBody

                let! finalGap = completeFullGap durable xTrace gapStart evidence.Gap
                return Some(LifecycleWorkRecord.materialize openingMaterial frames finalGap includeOpening)
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
                return!
                    materializeOpeningEvidence durable snapshot session includeOpening coverageOverride xTrace opening
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

    /// COMPANION-015: (Previous, Next] overlaps [Start, End); Next is inclusive-through.
    let private framesOverlappingRange (range: XTraceRange) (frames: BlogFrame list) =
        let startSequence = range |> XTraceRange.startInclusive |> XTraceCursor.sequence
        let endSequence = range |> XTraceRange.endExclusive |> XTraceCursor.sequence

        frames
        |> List.filter (fun frame ->
            frame.CoveredFromSequence + 1L < endSequence
            && frame.CoveredThroughSequence >= startSequence)

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

    let private clampCoverageToRange (coverage: RecordCoverage) (range: XTraceRange) =
        let cursor = RecordCoverage.ingestedThrough coverage
        let start = XTraceRange.startInclusive range
        let finish = XTraceRange.endExclusive range

        if XTraceCursor.isBefore cursor start then
            RecordCoverage.create start
        elif XTraceCursor.isAfter cursor finish then
            RecordCoverage.create finish
        else
            coverage

    let private completeBoundedGap durable xTrace workRange range terminalProviderRun renderedGap =
        task {
            match terminalForBoundedRange terminalProviderRun range xTrace with
            | None -> return renderedGap
            | Some terminal ->
                return! appendTerminalFallback durable (XTraceProjection.slice workRange xTrace) renderedGap terminal
        }

    let private materializeBoundedOpeningEvidence durable session range terminalProviderRun xTrace opening =
        task {
            let blog = session.Blog |> Option.defaultValue BlogProjection.empty
            let workRange = boundedWorkRange terminalProviderRun range xTrace

            let! frames =
                BlogProjection.frames blog
                |> framesOverlappingRange workRange
                |> resolveFrames durable

            let coverage =
                blog.Coverage.IngestedThroughSequence
                |> XTraceCursor.create
                |> RecordCoverage.create
                |> fun value -> clampCoverageToRange value workRange

            let gapStart =
                laterCursor (RecordCoverage.ingestedThrough coverage) (XTraceRange.startInclusive workRange)

            let gapRange = XTraceRange.create gapStart (XTraceRange.endExclusive workRange)
            let! gap = renderWorkRecordRange durable gapRange xTrace

            match gap with
            | Error _ -> return None
            | Ok renderedGap ->
                let! finalGap = completeBoundedGap durable xTrace workRange range terminalProviderRun renderedGap

                return Some(LifecycleWorkRecord.materialize opening frames finalGap false)
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

    let lifecycleWorkRecordBoundedForRun
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (range: XTraceRange)
        (providerRun: ProviderRunIdentity)
        : Task<string option> =
        match journal with
        | None -> Task.FromResult None
        | Some durable ->
            lifecycleWorkRecordBoundedFromSnapshotForRun
                durable
                (AgentJournal.snapshot durable)
                sessionId
                range
                providerRun
