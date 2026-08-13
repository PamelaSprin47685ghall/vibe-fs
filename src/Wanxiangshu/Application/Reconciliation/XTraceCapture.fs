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

    type private TraceSourcePart =
        { Part: SemanticPart
          ToolCallId: ToolCallId option
          HostToolPartId: HostToolPartId option }

    type private TraceSourceMessage =
        { Role: string
          ProviderRun: ProviderRunIdentity option
          Parts: TraceSourcePart list }

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
    /// (measured: `BlogObservationCommitted` was rejected as "consumed nothing" when
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
    let private semanticPartFromWire (part: WirePart) : SemanticPart =
        match part with
        | WireText text -> SemanticText text
        | WireReasoning text -> SemanticReasoning text
        | WireToolCall(_, name, args) -> SemanticToolCall(name, args)
        | WireToolResult(_, result) -> SemanticToolResult result
        | WireMedia(mediaType, digest) -> SemanticMedia(mediaType, digest)

    let private captureSources
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (messages: TraceSourceMessage list)
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

            messages
            |> List.iteri (fun turnIndex message ->
                message.Parts
                |> List.iteri (fun partIndex source ->
                    // g:N isolates Host renumbering after ContextReanchored (HOST-006).
                    // PrefixRebase does not grow ReanchoredRuns, so ordinary epoch
                    // promotion keeps the same generation and stays idempotent.
                    let provenance = sprintf "g:%d/turn:%d/part:%d" generation turnIndex partIndex

                    if not (Set.contains provenance recorded) then
                        cursor <- cursor + 1L
                        let kind, toolName, body = partShape source.Part

                        match durable.WriteBlob body with
                        | Error error ->
                            // PERSIST-003: a part that cannot be proven stored must
                            // fail closed. Swallowing here desyncs XTrace from the
                            // provider projection and later rejects BlogObservationCommitted.
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
                                   ProviderRun = message.ProviderRun
                                   ToolCallId = source.ToolCallId
                                   HostToolPartId = source.HostToolPartId |}
                            |> appendFact durable sessionId message.ProviderRun))

            Some(xTraceOf durable sessionId)

    /// STRENGTH-008: whether this XTrace can accept a historical insertion
    /// without positional provenance drift. Empty traces are eligible; once a
    /// runtime has captured parts, every provenance must be Host-message based.
    /// Legacy `g:N/turn:M/part:P` traces remain readable but force Strength K0.
    let supportsStableInsertion (journal: AgentJournal option) (sessionId: SessionId) : bool =
        match journal with
        | None -> false
        | Some durable ->
            let existing = xTraceOf durable sessionId

            existing.Parts
            |> List.forall (fun part -> part.Provenance.Contains("/msg:", StringComparison.Ordinal))

    let private captureSourcesStable
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (messageIds: string list)
        (messages: TraceSourceMessage list)
        : Result<XTraceProjectionState option, string> =
        match journal with
        | None -> Ok None
        | Some durable ->
            let existing = xTraceOf durable sessionId

            if not (supportsStableInsertion journal sessionId) then
                Error "legacy positional XTrace cannot accept stable historical insertion"
            elif List.length messageIds <> List.length messages then
                Error "stable XTrace message identity cardinality does not match semantic projection"
            elif messageIds |> List.exists String.IsNullOrWhiteSpace then
                Error "stable XTrace requires a non-empty Host id for every semantic message"
            elif (messageIds |> Set.ofList |> Set.count) <> List.length messageIds then
                Error "stable XTrace requires unique Host message ids"
            else
                let generation = captureGeneration durable sessionId

                let recorded =
                    existing.Parts |> List.map (fun part -> part.Provenance) |> Set.ofList

                // DSL-MUTABLE: algorithm-scratch — monotone stable-capture cursor
                let mutable cursor = XTraceProjection.headSequence existing
                // DSL-MUTABLE: algorithm-scratch — first write failure during one capture fold
                let mutable failure: string option = None

                List.zip messageIds messages
                |> List.iteri (fun turnIndex (messageId, message) ->
                    if Option.isNone failure then
                        message.Parts
                        |> List.iteri (fun partIndex source ->
                            if Option.isNone failure then
                                let provenance = sprintf "g:%d/msg:%s/part:%d" generation messageId partIndex

                                if not (Set.contains provenance recorded) then
                                    cursor <- cursor + 1L
                                    let kind, toolName, body = partShape source.Part

                                    match durable.WriteBlob body with
                                    | Error error ->
                                        failure <- Some(sprintf "XTrace part blob write failed: %s" error)
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
                                               ProviderRun = message.ProviderRun
                                               ToolCallId = source.ToolCallId
                                               HostToolPartId = source.HostToolPartId |}
                                        |> appendFact durable sessionId message.ProviderRun))

                match failure with
                | Some error -> Error error
                | None -> Ok(Some(xTraceOf durable sessionId))

    /// Write last_words as a normal assistant text part so a completed Life's
    /// LWR Recent work contains them. Dedicated provenance avoids colliding
    /// with g:N/turn:M/part:P capture. Caller supplies an already-written blob.
    let captureLastWords
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (textRef: BlobRef)
        (textDigest: BlobDigest)
        (providerRun: ProviderRunIdentity)
        =
        match journal with
        | None -> ()
        | Some durable ->
            let existing = xTraceOf durable sessionId
            let generation = captureGeneration durable sessionId
            let provenance = sprintf "g:%d/last_words" generation
            let recorded =
                existing.Parts |> List.map (fun part -> part.Provenance) |> Set.ofList

            if not (Set.contains provenance recorded) then
                let cursor = XTraceProjection.headSequence existing + 1L

                let turn, partIndex =
                    match List.tryLast existing.Parts with
                    | Some last -> last.Turn + 1, 0
                    | None -> 0, 0

                CompanionFact.XTracePartAppended
                    {| SessionId = sessionId
                       CursorSequence = cursor
                       Role = "assistant"
                       Turn = turn
                       PartIndex = partIndex
                       Kind = "text"
                       ToolName = None
                       TextRef = textRef
                       TextDigest = textDigest
                       Provenance = provenance
                       ProviderRun = Some providerRun
                       ToolCallId = None
                       HostToolPartId = None |}
                |> appendFact durable sessionId (Some providerRun)

    let captureProjection
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (projection: ProviderSemanticProjection)
        : XTraceProjectionState option =
        projection.Messages
        |> List.map (fun message ->
            { Role = message.Role
              ProviderRun = None
              Parts =
                message.Parts
                |> List.map (fun part ->
                    { Part = part
                      ToolCallId = None
                      HostToolPartId = None }) })
        |> captureSources journal sessionId

    /// Capture an already-decoded transform view with the assistant message and
    /// Host ToolPart identities that localize a V1 tool invocation.
    let captureMessageView
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (messages: ProviderWireCapture.CapturedWireMessage list)
        : XTraceProjectionState option =
        messages
        |> List.map (fun message ->
            { Role = message.Role
              ProviderRun = message.ProviderRun
              Parts =
                message.Parts
                |> List.map (fun captured ->
                    let toolCallId =
                        match captured.WirePart with
                        | WireToolCall(callId, _, _)
                        | WireToolResult(callId, _) -> Some callId
                        | _ -> None

                    { Part = semanticPartFromWire captured.WirePart
                      ToolCallId = toolCallId
                      HostToolPartId = captured.HostToolPartId }) })
        |> captureSources journal sessionId

    let captureProjectionStable
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (messageIds: string list)
        (projection: ProviderSemanticProjection)
        : Result<XTraceProjectionState option, string> =
        projection.Messages
        |> List.map (fun message ->
            { Role = message.Role
              ProviderRun = None
              Parts =
                message.Parts
                |> List.map (fun part ->
                    { Part = part
                      ToolCallId = None
                      HostToolPartId = None }) })
        |> captureSourcesStable journal sessionId messageIds

    let captureMessageViewStable
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (messageIds: string list)
        (messages: ProviderWireCapture.CapturedWireMessage list)
        : Result<XTraceProjectionState option, string> =
        messages
        |> List.map (fun message ->
            { Role = message.Role
              ProviderRun = message.ProviderRun
              Parts =
                message.Parts
                |> List.map (fun captured ->
                    let toolCallId =
                        match captured.WirePart with
                        | WireToolCall(callId, _, _)
                        | WireToolResult(callId, _) -> Some callId
                        | _ -> None

                    { Part = semanticPartFromWire captured.WirePart
                      ToolCallId = toolCallId
                      HostToolPartId = captured.HostToolPartId }) })
        |> captureSourcesStable journal sessionId messageIds
