namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
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
                AgentFact.OpeningPromptCaptured
                    {| SessionId = sessionId
                       AssignmentText = assignmentText
                       AuthoritativeRequirements = authoritativeRequirements
                       ProviderRun = None |}
                |> appendFact durable sessionId None

    /// COMPANION-003: capture the terminal output verbatim as the XTrace's last
    /// segment. Idempotent — the first capture wins (PERSIST-010).
    let captureTerminal (journal: AgentJournal option) (turn: ReconciledTurn) =
        match journal with
        | None -> ()
        | Some durable ->
            let existing = xTraceOf durable turn.SessionId

            if existing.Terminal.IsNone then
                // COMPANION-003: TerminalOutputRaw = formal text + host-visible
                // reasoning only. partsSessionText drops tool call/result so
                // LWR Final output stays free of raw tools and matches
                // AgentRunResult.TerminalText.
                let text = CompletedTurnClassifier.partsSessionText turn.Parts

                if not (String.IsNullOrWhiteSpace text) then
                    match durable.WriteBlob text with
                    | Error error ->
                        // PERSIST-003: the terminal segment is non-retryable (the
                        // final output never passes a transform again), so a blob
                        // it cannot prove committed must fail closed rather than
                        // silently produce an LWR missing its Final output.
                        raise (InvalidOperationException(sprintf "XTrace terminal blob write failed: %s" error))
                    | Ok blob ->
                        AgentFact.TerminalOutputCaptured
                            {| SessionId = turn.SessionId
                               TextRef = blob.BlobRef
                               TextDigest = blob.BlobDigest
                               ProviderRun = turn.ProviderRun |}
                        |> appendFact durable turn.SessionId (Some turn.ProviderRun)

    /// COMPANION-003 / EXEC-006 / EXEC-008: session LifecycleWorkRecord as opaque text.
    ///
    /// `includeOpening`:
    /// - parent → child background: true（子需要父任务上下文）
    /// - child → parent join: false（布置者已知任务，Opening 不回传）
    ///
    /// Opening 必须仍已 captured（否则 LWR 未定义 → None）；标志只控制渲染。
    /// Same materialiser for frames/gap/terminal; no B-else-A branch.
    let lifecycleWorkRecord
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (includeOpening: bool)
        : string option =
        match journal with
        | None -> None
        | Some durable ->
            let snapshot = AgentJournal.snapshot durable

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
                                | "tool_call" ->
                                    part.ToolName |> Option.map (fun name -> SemanticToolCall(name, body))
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

                let terminal =
                    match xTrace.Terminal with
                    | Some(textRef, textDigest) ->
                        match durable.Writer.BlobWriter.Read textRef with
                        | Ok text when HostDigest.sha256Hex text = BlobDigest.value textDigest -> Some text
                        | _ -> None
                    | None -> None

                match xTrace.Opening with
                | None -> None
                | Some opening ->
                    let coverage =
                        { IngestedThrough = { Sequence = blog.Coverage.IngestedThroughSequence } }

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
                            AgentFact.XTracePartAppended
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
