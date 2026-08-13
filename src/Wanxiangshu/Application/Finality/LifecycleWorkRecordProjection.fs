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
/// Same materialiser for frames/gap/terminal; no B-else-A branch.
///
/// `coverageOverride`:
/// - None → use blog.Coverage (incremental / compressed-frames gap).
/// - Some → force that coverage for gapStart (blessing wants full canonical:
///   IngestedThrough = origin so gap starts at openingEnd).
module LifecycleWorkRecordProjection =

    /// Resolve Y-compressed Chronicle frames from blobs, oldest first.
    let private resolveFrames (durable: AgentJournal) (blog: BlogProjectionState) : string list =
        blog.Frames
        |> List.choose (fun frame ->
            match durable.Writer.BlobWriter.Read frame.TextRef with
            | Ok text when HostDigest.sha256Hex text = BlobDigest.value frame.Digest -> Some text
            | _ -> None)

    /// Resolve XTrace part bodies into semantic items (single mapper; a part
    /// that fails its digest check is dropped, matching the canonical path).
    let private resolveTrace (durable: AgentJournal) (xTrace: XTraceProjectionState) : XTraceItem list =
        xTrace.Parts
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

    let private resolveTerminal (durable: AgentJournal) (terminalRef: (BlobRef * BlobDigest) option) : string option =
        match terminalRef with
        | Some(textRef, textDigest) ->
            match durable.Writer.BlobWriter.Read textRef with
            | Ok text when HostDigest.sha256Hex text = BlobDigest.value textDigest -> Some text
            | _ -> None
        | None -> None

    let lifecycleWorkRecordFromSnapshotWithTerminal
        (durable: AgentJournal)
        (snapshot: ProjectionSet)
        (sessionId: SessionId)
        (includeOpening: bool)
        (terminalOverride: (BlobRef * BlobDigest) option)
        (coverageOverride: RecordCoverage option)
        : string option =
        match AgentProjection.tryFind sessionId snapshot.AgentProjections with
        | None -> None
        | Some session ->
            let xTrace = session.XTrace |> Option.defaultValue XTraceProjection.empty
            let blog = session.Blog |> Option.defaultValue BlogProjection.empty

            let frames = resolveFrames durable blog

            let trace = resolveTrace durable xTrace

            let terminalRef =
                match terminalOverride with
                | Some specifiedTerminal -> Some specifiedTerminal
                | None -> xTrace.Terminal

            let terminal = resolveTerminal durable terminalRef

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

                // Terminal lives outside the trace parts; a head cursor keeps
                // materialize's terminal-exclusion filter from touching any gap
                // item while still carrying the text into the Closing report
                // section.
                let terminalItems =
                    terminal
                    |> Option.map (fun text ->
                        [ { Cursor = XTrace.head trace
                            Provenance = "terminal"
                            Role = "assistant"
                            Part = SemanticText text } ])
                    |> Option.defaultValue []

                Some(
                    LifecycleWorkRecord.materialize
                        openingMaterial
                        frames
                        trace
                        coverage
                        openingEnd
                        terminalItems
                        includeOpening
                )

    let lifecycleWorkRecordFromSnapshot
        (durable: AgentJournal)
        (snapshot: ProjectionSet)
        (sessionId: SessionId)
        (includeOpening: bool)
        : string option =
        lifecycleWorkRecordFromSnapshotWithTerminal durable snapshot sessionId includeOpening None None

    let lifecycleWorkRecord
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (includeOpening: bool)
        : string option =
        match journal with
        | None -> None
        | Some durable ->
            lifecycleWorkRecordFromSnapshot durable (AgentJournal.snapshot durable) sessionId includeOpening

    /// EXEC-031: per-invocation bounded LWR for reusable SyncDelegate children
    /// (includeOpening=false). Chronicle frames + Recent-work TRACE are sliced to
    /// the invocation's XTrace range; the Closing report comes from the
    /// just-captured xTrace.Terminal. Reuses LifecycleWorkRecord.materialize —
    /// no second renderer.
    ///
    /// TRAP: the canonical projector parks Terminal at `XTrace.head` (one past
    /// the last part). The bounded trace is sliced to [StartInclusive, EndExclusive);
    /// `XTrace.head slicedTrace` = EndExclusive, which is never a part cursor, so
    /// materialize's terminal-exclusion filter keeps every gap item while the
    /// terminal text still renders as the Closing report. A full-lifecycle
    /// projection here would leak prior invocations on a reused child.
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

            let frames = resolveFrames durable blog

            // Recent-work TRACE sliced to the invocation's range; the Closing
            // report is carried separately so prior invocations never appear.
            let trace = resolveTrace durable xTrace |> XTrace.sliceBetween range.StartInclusive range.EndExclusive

            let terminal = resolveTerminal durable xTrace.Terminal

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

                // Terminal lives outside the sliced parts; a head cursor keeps
                // materialize's terminal-exclusion filter from touching any gap
                // item while still carrying the text into the Closing report.
                let terminalItems =
                    terminal
                    |> Option.map (fun text ->
                        [ { Cursor = XTrace.head trace
                            Provenance = "terminal"
                            Role = "assistant"
                            Part = SemanticText text } ])
                    |> Option.defaultValue []

                Some(
                    LifecycleWorkRecord.materialize
                        opening
                        frames
                        trace
                        coverageClamped
                        range.StartInclusive
                        terminalItems
                        (* includeOpening = *) false
                )

    let lifecycleWorkRecordBounded
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (range: MagicTodoLwr.BoundedRange)
        : string option =
        match journal with
        | None -> None
        | Some durable ->
            lifecycleWorkRecordBoundedFromSnapshot durable (AgentJournal.snapshot durable) sessionId range
