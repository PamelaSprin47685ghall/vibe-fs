namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// COMPANION-003 / HOST-005 / COMPANION-012: the single semantic capture path.
///
/// Every prompt/assistant/reasoning/tool part enters the XTrace through exactly
/// one mapper, so no two independent parser/renderer pairs can disagree about a
/// segment. `TerminalSessionA` is gone: the terminal output is captured as the
/// XTrace's last segment (TerminalOutputCaptured), not as a parallel A text.
module XTraceCapture =

    /// The one `MessagePart → SemanticPart` mapper. `Activity` parts are
    /// transport bookkeeping and carry no semantics, so they are dropped here.
    let semanticPart (part: MessagePart) : SemanticPart option =
        match part with
        | MessagePart.Text text -> Some(SemanticText text)
        | MessagePart.Reasoning text -> Some(SemanticReasoning text)
        | MessagePart.ToolCall(_callId, name, args) -> Some(SemanticToolCall(name, args))
        | MessagePart.ToolResult(_callId, result) -> Some(SemanticToolResult result)
        | MessagePart.Activity _ -> None

    /// The `SemanticPart → (kind, toolName, body)` split that the durable fact
    /// needs: body goes to a blob, kind and tool name ride on the journal line.
    let private partShape (part: SemanticPart) : string * string option * string =
        match part with
        | SemanticText text -> "text", None, text
        | SemanticReasoning text -> "reasoning", None, text
        | SemanticToolCall(name, args) -> "tool_call", Some name, args
        | SemanticToolResult result -> "tool_result", None, result
        | SemanticMedia(mediaType, _digest) -> "media_omitted", None, (mediaType |> Option.defaultValue "")

    let private xTraceOf (journal: AgentJournal) (sessionId: SessionId) =
        AgentJournal.snapshot journal
        |> fun projection -> AgentProjection.tryFind sessionId projection.AgentProjections
        |> Option.bind (fun session -> session.XTrace)
        |> Option.defaultValue XTraceProjection.empty

    let private appendFact
        (journal: AgentJournal)
        (sessionId: SessionId)
        (run: ProviderRunIdentity option)
        (fact: AgentFact)
        =
        // PERSIST-003: an append that cannot be proven committed is a fail-closed
        // condition, not something to swallow — the caller would keep running
        // against a journal that no longer agrees with its own view.
        AgentJournal.appendAgent (StreamId.Session sessionId) run fact journal
        |> Result.mapError (fun failure ->
            raise (
                InvalidOperationException(sprintf "XTrace append failed: %s" (JournalAppendFailure.describe failure))
            ))
        |> ignore

    /// COMPANION-003: capture the opening task verbatim. Idempotent — a session
    /// with an opening already captured is left alone (PERSIST-010), which makes
    /// a replayed chat.message harmless.
    let captureOpening
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (assignmentText: string)
        (authoritativeRequirements: string list)
        =
        match journal with
        | None -> ()
        | Some durable ->
            let existing = xTraceOf durable sessionId

            if existing.Opening.IsNone && not (String.IsNullOrWhiteSpace assignmentText) then
                CompanionFact.OpeningPromptCaptured
                    {| SessionId = sessionId
                       AssignmentText = assignmentText
                       AuthoritativeRequirements = authoritativeRequirements
                       ProviderRun = None |}
                |> appendFact durable sessionId None

    /// COMPANION-003 / EXEC-009: capture terminal text into XTrace.
    /// Same durability as `captureTerminal`; used when the caller already holds
    /// `AgentRunResult.TerminalText` (e.g. oneshot Completed before journal write).
    /// Idempotent replay (same text) is a no-op.
    let captureTerminalText
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (text: string)
        (providerRun: ProviderRunIdentity)
        =
        match journal with
        | None -> ()
        | Some durable ->
            if not (String.IsNullOrWhiteSpace text) then
                let existing = xTraceOf durable sessionId

                let isReplay =
                    existing.Terminal
                    |> Option.exists (fun (_, digest) -> HostDigest.sha256Hex text = BlobDigest.value digest)

                if not isReplay then
                    match durable.WriteBlob text with
                    | Error error ->
                        // PERSIST-003: the terminal segment is non-retryable (the
                        // final output never passes a transform again), so a blob
                        // it cannot prove committed must fail closed rather than
                        // silently produce an LWR missing its Final output.
                        raise (InvalidOperationException(sprintf "XTrace terminal blob write failed: %s" error))
                    | Ok blob ->
                        CompanionFact.TerminalOutputCaptured
                            {| SessionId = sessionId
                               TextRef = blob.BlobRef
                               TextDigest = blob.BlobDigest
                               ProviderRun = providerRun |}
                        |> appendFact durable sessionId (Some providerRun)

    /// COMPANION-003 / EXEC-009: capture the terminal output verbatim as the
    /// XTrace's last segment. Idempotent replay (same text) is a no-op;
    /// a different text overwrites — subagent reuse produces a new terminal
    /// per work unit on the same child session.
    let captureTerminal (journal: AgentJournal option) (turn: ReconciledTurn) =
        // COMPANION-003: TerminalOutputRaw = formal text + host-visible
        // reasoning only. partsSessionText drops tool call/result so
        // LWR Final output stays free of raw tools and matches
        // AgentRunResult.TerminalText.
        let text = CompletedTurnClassifier.partsSessionText turn.Parts
        captureTerminalText journal turn.SessionId text turn.ProviderRun

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
                // item while still carrying the text into the Final output
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

    /// HOST-006: Host turn indices restart after reanchor; XTrace does not.
    /// Provenance must carry the reanchor generation or post-compaction turns
    /// collide with pre-compaction `turn:0/part:0` and never append — RecordCoverage
    /// then maps a Host cursor onto a dead numbering and stages Next≤Prev.
    let private captureGeneration (journal: AgentJournal) (sessionId: SessionId) : int =
        AgentJournal.snapshot journal
        |> fun projection -> AgentProjection.tryFind sessionId projection.AgentProjections
        |> Option.bind (fun session -> session.PrefixEpoch)
        |> Option.map (fun epoch -> Set.count epoch.ReanchoredRuns)
        |> Option.defaultValue 0

    /// COMPANION-003 / COMPANION-007: synchronise the XTrace with the provider's
    /// semantic projection at the transform boundary.
    ///
    /// The Blogger chunker works in semantic (turn/part) coordinates against the
    /// same projection, so the XTrace must mirror it part-for-part or the
    /// `SemanticCursor → XTrace cursor` mapping cannot advance monotonically
    /// (measured: `BlogEntryCommitted` was rejected as "consumed nothing" when
    /// the XTrace lagged the projection).
    ///
    /// Idempotent by `(reanchorGen, turn, part)` provenance: the same Host view is
    /// re-observed every transform without duplicating the trace; a reanchor opens
    /// a new generation so renumbered Host turns append instead of colliding.
    ///
    /// Returns the updated XTrace state (or `None` without a journal), so the
    /// caller can refresh the in-memory Companion mirror — otherwise the chunker
    /// keeps mapping against the stale trace captured at construction and re-reads
    /// the projection head every round.
    let captureProjection
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (projection: ProviderSemanticProjection)
        : XTraceProjectionState option =
        match journal with
        | None -> None
        | Some durable ->
            let existing = xTraceOf durable sessionId
            let generation = captureGeneration durable sessionId

            let recorded =
                existing.Parts |> List.map (fun part -> part.Provenance) |> Set.ofList

            // DSL-MUTABLE: algorithm-scratch — monotone projection cursor advanced by the fold
            let mutable cursor = XTraceProjection.headSequence existing

            projection.Messages
            |> List.iteri (fun turnIndex message ->
                message.Parts
                |> List.iteri (fun partIndex part ->
                    // g:N isolates Host renumbering after ContextReanchored (HOST-006).
                    // PrefixRebase does not grow ReanchoredRuns, so ordinary epoch
                    // promotion keeps the same generation and stays idempotent.
                    let provenance = sprintf "g:%d/turn:%d/part:%d" generation turnIndex partIndex

                    if not (Set.contains provenance recorded) then
                        cursor <- cursor + 1L
                        let kind, toolName, body = partShape part

                        match durable.WriteBlob body with
                        | Error error ->
                            // PERSIST-003: a part that cannot be proven stored must
                            // fail closed. Swallowing here desyncs XTrace from the
                            // provider projection and later rejects BlogEntryCommitted.
                            raise (InvalidOperationException(sprintf "XTrace part blob write failed: %s" error))
                        | Ok blob ->
                            CompanionFact.XTracePartAppended
                                {| SessionId = sessionId
                                   CursorSequence = cursor
                                   Role = message.Role
                                   Turn = turnIndex
                                   PartIndex = partIndex
                                   Kind = kind
                                   ToolName = toolName
                                   TextRef = blob.BlobRef
                                   TextDigest = blob.BlobDigest
                                   Provenance = provenance
                                   ProviderRun = None |}
                            |> appendFact durable sessionId None))

            Some(xTraceOf durable sessionId)
