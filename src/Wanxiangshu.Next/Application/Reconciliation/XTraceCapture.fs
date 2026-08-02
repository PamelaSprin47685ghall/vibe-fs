namespace Wanxiangshu.Next.OpenCode

open System
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Domain.ProviderProjection
open Wanxiangshu.Next.Host
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

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
                let text = CompletedTurnClassifier.partsText turn.Parts

                if not (String.IsNullOrWhiteSpace text) then
                    match durable.WriteBlob text with
                    | Error _ -> ()
                    | Ok blob ->
                        AgentFact.TerminalOutputCaptured
                            {| SessionId = turn.SessionId
                               TextRef = blob.BlobRef
                               TextDigest = blob.BlobDigest
                               ProviderRun = turn.ProviderRun |}
                        |> appendFact durable turn.SessionId (Some turn.ProviderRun)

    /// HOST-005 replacement: the session's terminal output as optional text.
    ///
    /// Reads the durable XTrace's captured terminal blob. This replaces the old
    /// in-memory session-wide A accumulation (`TerminalSessionA.fullText`): the
    /// terminal is a durable lifecycle segment, not a parallel text channel.
    /// COMPANION-003 / EXEC-008: the parent's LifecycleWorkRecord as opaque text.
    ///
    /// The single materialisation used for a child's initial context: Opening
    /// verbatim, then the effective Y frames, then the X gap after the recorded
    /// coverage, then the terminal. There is no "B else A" branch — this is the
    /// same algorithm whether Y has produced zero frames or all of them.
    let parentWorkRecord (journal: AgentJournal option) (sessionId: SessionId) : string option =
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
                    // Opening lives outside the trace parts, so the gap starts at
                    // the recorded coverage (max with origin is identity).
                    let coverage =
                        { IngestedThrough = { Sequence = blog.Coverage.IngestedThroughSequence } }

                    Some(
                        LifecycleWorkRecord.render
                            { Opening = opening
                              Frames = frames
                              Gap = XTrace.sliceFrom coverage.IngestedThrough trace
                              Terminal = terminal }
                    )

    /// COMPANION-003 / COMPANION-007: synchronise the XTrace with the provider's
    /// semantic projection at the transform boundary.
    ///
    /// The Blogger chunker works in semantic (turn/part) coordinates against the
    /// same projection, so the XTrace must mirror it part-for-part or the
    /// `SemanticCursor → XTrace cursor` mapping in `CompanionJournalPort` cannot
    /// advance monotonically (measured: `BlogEntryCommitted` was rejected as
    /// "consumed nothing" when the XTrace lagged the projection).
    ///
    /// Idempotent by (turn, part) provenance: the same turn is re-observed on
    /// every later request, and re-observing must not duplicate the trace.
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

            let recorded =
                existing.Parts |> List.map (fun part -> part.Provenance) |> Set.ofList

            let mutable cursor = XTraceProjection.headSequence existing

            projection.Messages
            |> List.iteri (fun turnIndex message ->
                message.Parts
                |> List.iteri (fun partIndex part ->
                    let provenance = sprintf "turn:%d/part:%d" turnIndex partIndex

                    if not (Set.contains provenance recorded) then
                        cursor <- cursor + 1L
                        let kind, toolName, body = partShape part

                        match durable.WriteBlob body with
                        | Error _ -> ()
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
