namespace Wanxiangshu.Finality

open System
open Wanxiangshu.Domain
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel.Identity

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

    /// Resolve Y-compressed Chronicle frames from blobs, oldest first.
    let private resolveFrames (durable: AgentJournal) (frames: BlogFrame list) : string list =
        frames
        |> List.choose (fun frame ->
            match durable.Writer.BlobWriter.Read frame.TextRef with
            | Ok text when HostDigest.sha256Hex text = BlobDigest.value frame.Digest -> Some text
            | _ -> None)

    /// COMPANION-015: (Previous, Next] overlaps [Start, End); Next is inclusive-through.
    let private framesOverlappingRange (range: MagicTodoLwr.BoundedRange) (frames: BlogFrame list) =
        frames
        |> List.filter (fun frame ->
            frame.CoveredFromSequence < range.EndExclusive.Sequence
            && frame.CoveredThroughSequence >= range.StartInclusive.Sequence)

    /// Resolve XTrace part bodies into semantic items (single mapper; a part
    /// that fails its digest check is dropped, matching the canonical path).
    let private resolveTrace (durable: AgentJournal) (xTrace: XTraceProjectionState) : XTraceItem list =
        XTraceProjection.parts xTrace
        |> List.choose (fun part ->
            durable.Writer.BlobWriter.Read part.TextRef
            |> Result.toOption
            |> Option.bind (fun body ->
                let semantic =
                    match part.Kind with
                    | "text" -> Some(SemanticText body)
                    | "reasoning" -> Some(SemanticReasoning body)
                    | "tool_call" -> part.ToolName |> Option.map (fun name -> SemanticToolCall(name, body))
                    | "tool_result" -> Some(SemanticToolResult body)
                    // COMPANION-003: omission markers are semantic parts of
                    // the XTrace; dropping them would make LWR gap/parent
                    // background lose media presence the model already saw.
                    | "media_omitted" ->
                        let mediaType = if String.IsNullOrWhiteSpace body then None else Some body

                        Some(SemanticMedia(mediaType, ""))
                    | _ -> None

                semantic
                |> Option.map (fun partValue ->
                    { Cursor = part.Cursor
                      Provenance = part.Provenance
                      Role = part.Role
                      Part = partValue })))

    let lifecycleWorkRecordFromSnapshot
        (durable: AgentJournal)
        (snapshot: ProjectionSet)
        (sessionId: SessionId)
        (includeOpening: bool)
        (coverageOverride: RecordCoverage option)
        : string option =
        match AgentProjection.tryFind sessionId snapshot.AgentProjections with
        | None -> None
        | Some session ->
            let xTrace = session.XTrace |> Option.defaultValue XTraceProjection.empty
            let blog = session.Blog |> Option.defaultValue BlogProjection.empty

            let frames = resolveFrames durable (BlogProjection.frames blog)

            let trace = resolveTrace durable xTrace

            match xTrace.Opening with
            | None -> None
            | Some opening ->
                let coverage =
                    match coverageOverride with
                    | Some forced -> forced
                    | None -> { IngestedThrough = { Sequence = blog.Coverage.IngestedThroughSequence } }

                let life =
                    session.ManagerLife |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)

                // TODO-001 / COMPANION-014: OpeningBoundary = WorkRecordStart when
                // Post-T1; else Immediate exclusive end after first XTrace part.
                let openingEnd =
                    match
                        life
                        |> Option.bind (fun current ->
                            ManagerOpeningFloor.workRecordStart
                                current
                                (MagicTodoProjection.tryLife current.LifeId snapshot.AgentProjections.MagicTodo)
                                xTrace)
                    with
                    | Some boundary -> boundary
                    | None ->
                        match trace with
                        | first :: _ -> { Sequence = first.Cursor.Sequence + 1L }
                        | [] -> XTrace.originCursor

                // Constitutive Opening interval after InitialCharge through Boundary
                // (BlindPlan T1 call/result ∈ OpeningMaterial; not Recent).
                let constitutiveItems =
                    match trace with
                    | first :: _ -> XTrace.sliceBetween { Sequence = first.Cursor.Sequence + 1L } openingEnd trace
                    | [] -> []

                let openingMaterial = LifecycleWorkRecord.withConstitutive opening constitutiveItems

                Some(LifecycleWorkRecord.materialize openingMaterial frames trace coverage openingEnd includeOpening)

    let lifecycleWorkRecord
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (includeOpening: bool)
        : string option =
        match journal with
        | None -> None
        | Some durable ->
            lifecycleWorkRecordFromSnapshot durable (AgentJournal.snapshot durable) sessionId includeOpening None

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
        : string option =
        match AgentProjection.tryFind sessionId snapshot.AgentProjections with
        | None -> None
        | Some session ->
            let xTrace = session.XTrace |> Option.defaultValue XTraceProjection.empty
            let blog = session.Blog |> Option.defaultValue BlogProjection.empty

            // COMPANION-015 ④/⑩: Chronicle = Y frames overlapping this invocation.
            let frames =
                BlogProjection.frames blog
                |> framesOverlappingRange range
                |> resolveFrames durable

            // Recent-work TRACE sliced to the invocation's range so prior
            // invocations never appear.
            let trace =
                resolveTrace durable xTrace
                |> XTrace.sliceBetween range.StartInclusive range.EndExclusive

            match xTrace.Opening with
            | None -> None
            | Some opening ->
                let coverage =
                    { IngestedThrough = { Sequence = blog.Coverage.IngestedThroughSequence } }

                // Gap start inside the bounded window: max(coverage, range start),
                // so an older coverage never pulls a prior invocation into view.
                let coverageClamped =
                    if coverage.IngestedThrough.Sequence < range.StartInclusive.Sequence then
                        { IngestedThrough = range.StartInclusive }
                    elif coverage.IngestedThrough.Sequence > range.EndExclusive.Sequence then
                        { IngestedThrough = range.EndExclusive }
                    else
                        coverage

                Some(
                    LifecycleWorkRecord.materialize
                        opening
                        frames
                        trace
                        coverageClamped
                        range.StartInclusive
                        (* includeOpening = *) false
                )

    let lifecycleWorkRecordBounded
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (range: MagicTodoLwr.BoundedRange)
        : string option =
        match journal with
        | None -> None
        | Some durable -> lifecycleWorkRecordBoundedFromSnapshot durable (AgentJournal.snapshot durable) sessionId range
