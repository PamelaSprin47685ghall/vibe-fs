namespace Wanxiangshu.Mission.WorkRecord

open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength.Persistence

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Trace
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation.Identity

/// COMPANION-003 / EXEC-006 / EXEC-008: session LifecycleWorkRecord as opaque text.
///
/// `includeOpening`:
/// - parent → child background: true（子需要父任务上下文）
/// - child → parent: false（布置者已知任务，Opening 不回传）
///
/// Opening 必须仍已 captured（否则 LWR 未定义 → None）；标志只控制渲染。
/// Same materialiser for frames/gap; no B-else-A branch.
///
/// `coverageOverride`:
/// - None → use blog.Coverage (incremental / compressed-frames gap).
/// - Some → force that coverage for gapStart (blessing wants full canonical:
///   IngestedThrough = origin so gap starts at openingEnd).
module LifecycleWorkRecordProjection =

    let private tryResolveFrameText (durable: AgentJournal) (frame: BlogFrame) : Task<string option> =
        task {
            match! durable.Writer.BlobWriter.Read frame.TextRef with
            | Ok text when HostDigest.sha256Hex text = BlobDigest.value frame.Digest -> return Some text
            | _ -> return None
        }

    /// Resolve Y-compressed Chronicle frames from blobs, oldest first.
    let private resolveFrames (durable: AgentJournal) (frames: BlogFrame list) : Task<string list> =
        task {
            // DSL-MUTABLE: algorithm-scratch — resolved frame accumulator
            let resolved = ResizeArray<string>()

            for frame in frames do
                let! textOpt = tryResolveFrameText durable frame
                textOpt |> Option.iter resolved.Add

            return resolved |> Seq.toList
        }

    let private tryReadPartBody (durable: AgentJournal) (part: XTracePartRef) : Task<string option> =
        task {
            match! durable.Writer.BlobWriter.Read part.TextRef with
            | Ok body when HostDigest.sha256Hex body = BlobDigest.value part.TextDigest -> return Some body
            | _ -> return None
        }

    let private readVerifiedTerminalText
        (durable: AgentJournal)
        (terminalRef: BlobRef)
        (terminalDigest: BlobDigest)
        : Task<string option> =
        task {
            match! durable.Writer.BlobWriter.Read terminalRef with
            | Error _ -> return None
            | Ok terminalText when HostDigest.sha256Hex terminalText <> BlobDigest.value terminalDigest -> return None
            | Ok terminalText when String.IsNullOrWhiteSpace terminalText -> return None
            | Ok terminalText -> return Some terminalText
        }

    let private isNaturalAssistant (part: XTracePartRef) =
        part.Role = "assistant" && (part.Kind = "text" || part.Kind = "reasoning")

    let private concatLatestAssistantBodies
        (durable: AgentJournal)
        (ordered: XTracePartRef list)
        (latest: XTracePartRef)
        : Task<string> =
        task {
            let relevant =
                ordered
                |> List.filter (fun part ->
                    part.Role = "assistant"
                    && part.Turn = latest.Turn
                    && part.Generation = latest.Generation
                    && (part.Kind = "text" || part.Kind = "reasoning"))

            // DSL-MUTABLE: algorithm-scratch — part body accumulator
            let bodies = ResizeArray<string>()

            for part in relevant do
                let! bodyOpt = tryReadPartBody durable part
                bodyOpt |> Option.iter bodies.Add

            return bodies |> Seq.toList |> String.concat "\n\n"
        }

    let private terminalAlreadyCaptured
        (durable: AgentJournal)
        (ordered: XTracePartRef list)
        (terminalText: string)
        : Task<bool> =
        task {
            match ordered |> List.filter isNaturalAssistant |> List.tryLast with
            | None -> return false
            | Some latest ->
                let! concat = concatLatestAssistantBodies durable ordered latest
                return concat = terminalText
        }

    let private withTerminalText
        (durable: AgentJournal)
        (xTrace: XTraceProjectionState)
        (trace: XTraceItem list)
        (terminalText: string)
        : Task<XTraceItem list> =
        task {
            let ordered = XTraceProjection.parts xTrace
            let! alreadyCaptured = terminalAlreadyCaptured durable ordered terminalText

            if alreadyCaptured then
                return trace
            else
                return
                    trace
                    @ [ { Cursor = { Sequence = XTraceProjection.head xTrace }
                          Provenance = "terminal-projection-fallback"
                          Role = "assistant"
                          Part = SemanticText terminalText } ]
        }

    let private appendTerminalFallback
        (durable: AgentJournal)
        (xTrace: XTraceProjectionState)
        (trace: XTraceItem list)
        (terminalRef: BlobRef)
        (terminalDigest: BlobDigest)
        : Task<XTraceItem list> =
        task {
            let! terminalOpt = readVerifiedTerminalText durable terminalRef terminalDigest

            match terminalOpt with
            | None -> return trace
            | Some terminalText -> return! withTerminalText durable xTrace trace terminalText
        }

    /// Full-lifecycle LWR fallback for Host-owned completion paths that consume a
    /// terminal before messages.transform has captured that final assistant turn.
    /// TerminalOutputCaptured is already durable; project it into Recent work only
    /// when the latest assistant turn's natural-language parts do not already
    /// reconstruct the exact same terminal bytes. No new XTrace fact/cursor is
    /// written, so normal captured turns remain unchanged and event counts do not grow.
    let private withTerminalFallback
        (durable: AgentJournal)
        (xTrace: XTraceProjectionState)
        (trace: XTraceItem list)
        : Task<XTraceItem list> =
        task {
            match xTrace.Terminal with
            | None -> return trace
            | Some(terminalRef, terminalDigest) ->
                return! appendTerminalFallback durable xTrace trace terminalRef terminalDigest
        }

    /// COMPANION-015: (Previous, Next] overlaps [Start, End); Next is inclusive-through.
    let private framesOverlappingRange (range: MagicTodoLwr.BoundedRange) (frames: BlogFrame list) =
        frames
        |> List.filter (fun frame ->
            frame.CoveredFromSequence < range.EndExclusive.Sequence
            && frame.CoveredThroughSequence >= range.StartInclusive.Sequence)

    let private semanticPart (kind: string) (body: string) (toolName: string option) =
        match kind with
        | "text" -> Some(SemanticText body)
        | "reasoning" -> Some(SemanticReasoning body)
        | "tool_call" -> toolName |> Option.map (fun name -> SemanticToolCall(name, body))
        | "tool_result" -> Some(SemanticToolResult body)
        // COMPANION-003: omission markers are semantic parts of
        // the XTrace; dropping them would make LWR gap/parent
        // background lose media presence the model already saw.
        | "media_omitted" ->
            let mediaType = if String.IsNullOrWhiteSpace body then None else Some body
            Some(SemanticMedia(mediaType, ""))
        | _ -> None

    let private tryResolveTraceItem (durable: AgentJournal) (part: XTracePartRef) : Task<XTraceItem option> =
        task {
            match! durable.Writer.BlobWriter.Read part.TextRef with
            | Error _ -> return None
            | Ok body ->
                return
                    semanticPart part.Kind body part.ToolName
                    |> Option.map (fun partValue ->
                        { Cursor = part.Cursor
                          Provenance = part.Provenance
                          Role = part.Role
                          Part = partValue })
        }

    /// Resolve XTrace part bodies into semantic items (single mapper; a part
    /// that fails its digest check is dropped, matching the canonical path).
    let private resolveTrace (durable: AgentJournal) (xTrace: XTraceProjectionState) : Task<XTraceItem list> =
        task {
            // DSL-MUTABLE: algorithm-scratch — resolved trace item accumulator
            let resolved = ResizeArray<XTraceItem>()

            for part in XTraceProjection.parts xTrace do
                let! itemOpt = tryResolveTraceItem durable part
                itemOpt |> Option.iter resolved.Add

            return resolved |> Seq.toList
        }

    let private coverageOrDefault (coverageOverride: RecordCoverage option) (blog: BlogProjectionState) =
        match coverageOverride with
        | Some forced -> forced
        | None -> { IngestedThrough = { Sequence = blog.Coverage.IngestedThroughSequence } }

    let private immediateOpeningEnd (trace: XTraceItem list) =
        match trace with
        | first :: _ -> { Sequence = first.Cursor.Sequence + 1L }
        | [] -> XTrace.originCursor

    let private resolveOpeningEnd
        (life: LifeProjection option)
        (snapshot: ProjectionSet)
        (xTrace: XTraceProjectionState)
        (trace: XTraceItem list)
        =
        match
            life
            |> Option.bind (fun current ->
                ManagerOpeningFloor.workRecordStart
                    current
                    (MagicTodoProjection.tryLife current.LifeId snapshot.AgentProjections.MagicTodo)
                    xTrace)
        with
        | Some boundary -> boundary
        | None -> immediateOpeningEnd trace

    let private constitutiveItemsFor (openingEnd: XTraceCursor) (trace: XTraceItem list) =
        match trace with
        | first :: _ -> XTrace.sliceBetween { Sequence = first.Cursor.Sequence + 1L } openingEnd trace
        | [] -> []

    let private materializeOpenedSession
        (durable: AgentJournal)
        (snapshot: ProjectionSet)
        (session: SessionAgentProjection)
        (includeOpening: bool)
        (coverageOverride: RecordCoverage option)
        : Task<string option> =
        task {
            let xTrace = session.XTrace |> Option.defaultValue XTraceProjection.empty
            let blog = session.Blog |> Option.defaultValue BlogProjection.empty

            let! frames = resolveFrames durable (BlogProjection.frames blog)
            let! rawTrace = resolveTrace durable xTrace
            let! trace = withTerminalFallback durable xTrace rawTrace

            match xTrace.Opening with
            | None -> return None
            | Some opening ->
                let coverage = coverageOrDefault coverageOverride blog

                let life =
                    session.ManagerLife |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)

                // TODO-001 / COMPANION-014: OpeningBoundary = WorkRecordStart when
                // Post-T1; else Immediate exclusive end after first XTrace part.
                let openingEnd = resolveOpeningEnd life snapshot xTrace trace

                // Constitutive Opening interval after InitialCharge through Boundary
                // (BlindPlan T1 call/result ∈ OpeningMaterial; not Recent).
                let constitutiveItems = constitutiveItemsFor openingEnd trace
                let openingMaterial = LifecycleWorkRecord.withConstitutive opening constitutiveItems

                return
                    Some(
                        LifecycleWorkRecord.materialize openingMaterial frames trace coverage openingEnd includeOpening
                    )
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

    let private clampCoverageToRange (coverage: RecordCoverage) (range: MagicTodoLwr.BoundedRange) =
        if coverage.IngestedThrough.Sequence < range.StartInclusive.Sequence then
            { IngestedThrough = range.StartInclusive }
        elif coverage.IngestedThrough.Sequence > range.EndExclusive.Sequence then
            { IngestedThrough = range.EndExclusive }
        else
            coverage

    let private materializeBoundedOpenedSession
        (durable: AgentJournal)
        (session: SessionAgentProjection)
        (range: MagicTodoLwr.BoundedRange)
        : Task<string option> =
        task {
            let xTrace = session.XTrace |> Option.defaultValue XTraceProjection.empty
            let blog = session.Blog |> Option.defaultValue BlogProjection.empty

            // COMPANION-015 ④/⑩: Chronicle = Y frames overlapping this invocation.
            let! frames =
                BlogProjection.frames blog
                |> framesOverlappingRange range
                |> resolveFrames durable

            // Recent-work TRACE sliced to the invocation's range so prior
            // invocations never appear.
            let! resolvedTrace = resolveTrace durable xTrace

            let trace =
                XTrace.sliceBetween range.StartInclusive range.EndExclusive resolvedTrace

            match xTrace.Opening with
            | None -> return None
            | Some opening ->
                let coverage =
                    { IngestedThrough = { Sequence = blog.Coverage.IngestedThroughSequence } }

                // Gap start inside the bounded window: max(coverage, range start),
                // so an older coverage never pulls a prior invocation into view.
                let coverageClamped = clampCoverageToRange coverage range

                return
                    Some(
                        LifecycleWorkRecord.materialize
                            opening
                            frames
                            trace
                            coverageClamped
                            range.StartInclusive
                            (* includeOpening = *) false
                    )
        }

    /// EXEC-031: per-invocation bounded LWR for reusable SyncDelegate children
    /// (includeOpening=false). Chronicle frames (interval overlap) + Recent-work
    /// TRACE are sliced to the invocation's XTrace range, including the last part.
    /// Reuses LifecycleWorkRecord.materialize — no second renderer.
    /// A full-lifecycle projection here would leak prior invocations on a reused child.
    let lifecycleWorkRecordBoundedFromSnapshot
        (durable: AgentJournal)
        (snapshot: ProjectionSet)
        (sessionId: SessionId)
        (range: MagicTodoLwr.BoundedRange)
        : Task<string option> =
        task {
            match AgentProjection.tryFind sessionId snapshot.AgentProjections with
            | None -> return None
            | Some session -> return! materializeBoundedOpenedSession durable session range
        }

    let lifecycleWorkRecordBounded
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (range: MagicTodoLwr.BoundedRange)
        : Task<string option> =
        match journal with
        | None -> Task.FromResult None
        | Some durable -> lifecycleWorkRecordBoundedFromSnapshot durable (AgentJournal.snapshot durable) sessionId range
