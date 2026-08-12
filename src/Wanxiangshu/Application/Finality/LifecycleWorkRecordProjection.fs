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

            // Resolve frame bodies from blobs, oldest first.
            let frames =
                blog.Frames
                |> List.choose (fun frame ->
                    match durable.Writer.BlobWriter.Read frame.TextRef with
                    | Ok text when HostDigest.sha256Hex text = BlobDigest.value frame.Digest -> Some text
                    | _ -> None)

            // Resolve XTrace part bodies into semantic items.
            let trace =
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

            let terminalRef =
                match terminalOverride with
                | Some specifiedTerminal -> Some specifiedTerminal
                | None -> xTrace.Terminal

            let terminal =
                match terminalRef with
                | Some(textRef, textDigest) ->
                    match durable.Writer.BlobWriter.Read textRef with
                    | Ok text when HostDigest.sha256Hex text = BlobDigest.value textDigest -> Some text
                    | _ -> None
                | None -> None

            match xTrace.Opening with
            | None -> None
            | Some opening ->
                let coverage =
                    match coverageOverride with
                    | Some forced -> forced
                    | None -> { IngestedThrough = { Sequence = blog.Coverage.IngestedThroughSequence } }

                // The opening is the first XTrace part (turn:0/part:0, captured
                // at the first transform), so the gap must start AFTER it —
                // otherwise the opening renders twice: once in the Opening
                // section and again as the gap's first item (COMPANION-003).
                let openingEnd =
                    match trace with
                    | first :: _ -> { Sequence = first.Cursor.Sequence + 1L }
                    | [] -> XTrace.originCursor

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
                        opening
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
