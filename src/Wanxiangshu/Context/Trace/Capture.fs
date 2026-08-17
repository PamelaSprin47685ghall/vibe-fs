namespace Wanxiangshu.Context.Trace

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
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
open Wanxiangshu.Mission.WorkRecord
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
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

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
          HostPartId: HostMessagePartId option
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
        : Task<unit> =
        task {
            match! AgentJournal.appendAgent (StreamId.Session sessionId) run fact journal with
            | Ok _ -> return ()
            | Error failure ->
                return
                    raise (
                        InvalidOperationException(
                            sprintf "XTrace append failed: %s" (JournalAppendFailure.describe failure)
                        )
                    )
        }

    let private requireBlobWritten (context: string) (result: Result<'a, string>) : 'a =
        match result with
        | Ok blob -> blob
        | Error error -> raise (InvalidOperationException(sprintf "%s: %s" context error))

    let private mapBlobWriteError (context: string) (write: Task<Result<'a, string>>) : Task<Result<'a, string>> =
        task {
            let! result = write
            return result |> Result.mapError (fun error -> sprintf "%s: %s" context error)
        }

    /// Evidence → Decision: opening absent and assignment non-blank → capture.
    let private captureOpeningWhenAbsent
        (durable: AgentJournal)
        (sessionId: SessionId)
        (assignmentText: string)
        (authoritativeRequirements: string list)
        : Task<unit> =
        task {
            let existing = xTraceOf durable sessionId

            if existing.Opening.IsNone && not (String.IsNullOrWhiteSpace assignmentText) then
                do!
                    CompanionFact.OpeningPromptCaptured
                        {| SessionId = sessionId
                           AssignmentText = assignmentText
                           AuthoritativeRequirements = authoritativeRequirements
                           ProviderRun = None |}
                    |> appendFact durable sessionId None
        }

    /// COMPANION-003: capture the opening task verbatim. Idempotent — a session
    /// with an opening already captured is left alone (PERSIST-010), which makes
    /// a replayed chat.message harmless.
    let captureOpening
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (assignmentText: string)
        (authoritativeRequirements: string list)
        =
        task {
            match journal with
            | None -> return ()
            | Some durable -> do! captureOpeningWhenAbsent durable sessionId assignmentText authoritativeRequirements
        }

    let private isReplayTerminal (durable: AgentJournal) (sessionId: SessionId) (text: string) : bool =
        xTraceOf durable sessionId
        |> fun existing -> existing.Terminal
        |> Option.exists (fun (_, digest) -> HostDigest.sha256Hex text = BlobDigest.value digest)

    let private appendTerminalOutput
        (durable: AgentJournal)
        (sessionId: SessionId)
        (text: string)
        (providerRun: ProviderRunIdentity)
        : Task<unit> =
        task {
            let! writeResult = durable.WriteBlob text
            let blob = requireBlobWritten "XTrace terminal blob write failed" writeResult

            do!
                CompanionFact.TerminalOutputCaptured
                    {| SessionId = sessionId
                       TextRef = blob.BlobRef
                       TextDigest = blob.BlobDigest
                       ProviderRun = providerRun |}
                |> appendFact durable sessionId (Some providerRun)
        }

    /// Evidence → Decision: non-blank, non-replay terminal text → durable capture.
    let private captureTerminalTextWhenFresh
        (durable: AgentJournal)
        (sessionId: SessionId)
        (text: string)
        (providerRun: ProviderRunIdentity)
        : Task<unit> =
        task {
            if String.IsNullOrWhiteSpace text then
                return ()
            elif isReplayTerminal durable sessionId text then
                return ()
            else
                do! appendTerminalOutput durable sessionId text providerRun
        }

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
        task {
            match journal with
            | None -> return ()
            | Some durable -> do! captureTerminalTextWhenFresh durable sessionId text providerRun
        }

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

    type private PartCaptureWork =
        { Message: TraceSourceMessage
          TurnIndex: int
          PartIndex: int
          Source: TraceSourcePart
          Provenance: string
          LegacyStableProvenance: string option }

    let private enumeratePositionalCaptureWork
        (generation: int)
        (messages: TraceSourceMessage list)
        : PartCaptureWork list =
        messages
        |> List.mapi (fun turnIndex message ->
            message.Parts
            |> List.mapi (fun partIndex source ->
                { Message = message
                  TurnIndex = turnIndex
                  PartIndex = partIndex
                  Source = source
                  Provenance = sprintf "g:%d/turn:%d/part:%d" generation turnIndex partIndex
                  LegacyStableProvenance = None }))
        |> List.concat

    let private enumerateStableCaptureWork
        (generation: int)
        (messageIds: string list)
        (messages: TraceSourceMessage list)
        : PartCaptureWork list =
        List.zip messageIds messages
        |> List.mapi (fun turnIndex (messageId, message) ->
            message.Parts
            |> List.mapi (fun partIndex source ->
                let provenance =
                    match source.HostPartId with
                    | Some partId ->
                        sprintf
                            "g:%d/msg:%s/host-part:%s"
                            generation
                            messageId
                            (HostMessagePartId.value partId)
                    | None -> sprintf "g:%d/msg:%s/part:%d" generation messageId partIndex

                { Message = message
                  TurnIndex = turnIndex
                  PartIndex = partIndex
                  Source = source
                  Provenance = provenance
                  LegacyStableProvenance = Some(sprintf "g:%d/msg:%s/part:%d" generation messageId partIndex) }))
        |> List.concat

    /// One positional part: skip if provenance known; else blob + append (raises on blob error).
    let private appendPositionalPartIfAbsent
        (durable: AgentJournal)
        (sessionId: SessionId)
        (recorded: Set<string>)
        (nextCursor: unit -> int64)
        (work: PartCaptureWork)
        : Task<unit> =
        task {
            if Set.contains work.Provenance recorded then
                return ()
            else
                let cursor = nextCursor ()
                let kind, toolName, body = partShape work.Source.Part
                let! writeResult = durable.WriteBlob body
                let blob = requireBlobWritten "XTrace part blob write failed" writeResult

                do!
                    CompanionFact.XTracePartAppended
                        {| SessionId = sessionId
                           CursorSequence = cursor
                           Role = work.Message.Role
                           Turn = work.TurnIndex
                           PartIndex = work.PartIndex
                           Kind = kind
                           ToolName = toolName
                           TextRef = blob.BlobRef
                           TextDigest = blob.BlobDigest
                           Provenance = work.Provenance
                           ProviderRun = work.Message.ProviderRun
                           ToolCallId = work.Source.ToolCallId
                           HostToolPartId = work.Source.HostToolPartId |}
                    |> appendFact durable sessionId work.Message.ProviderRun
        }

    let private captureSourcesWithJournal
        (durable: AgentJournal)
        (sessionId: SessionId)
        (messages: TraceSourceMessage list)
        : Task<XTraceProjectionState option> =
        task {
            let existing = xTraceOf durable sessionId
            let generation = captureGeneration durable sessionId

            let recorded =
                existing.Parts |> List.map (fun part -> part.Provenance) |> Set.ofList
            // DSL-MUTABLE: algorithm-scratch — next durable cursor while appending one capture batch
            let mutable cursor = XTraceProjection.headSequence existing

            let nextCursor () =
                cursor <- cursor + 1L
                cursor

            let works = enumeratePositionalCaptureWork generation messages

            for work in works do
                do! appendPositionalPartIfAbsent durable sessionId recorded nextCursor work

            return Some(xTraceOf durable sessionId)
        }

    let private captureSources
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (messages: TraceSourceMessage list)
        : Task<XTraceProjectionState option> =
        task {
            match journal with
            | None -> return None
            | Some durable -> return! captureSourcesWithJournal durable sessionId messages
        }

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

    /// Evidence → Decision: stable insertion prerequisites (capability + id contract).
    let private validateStableCapturePrerequisites
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (messageIds: string list)
        (messages: TraceSourceMessage list)
        : Result<unit, string> =
        if not (supportsStableInsertion journal sessionId) then
            Error "legacy positional XTrace cannot accept stable historical insertion"
        elif List.length messageIds <> List.length messages then
            Error "stable XTrace message identity cardinality does not match semantic projection"
        elif messageIds |> List.exists String.IsNullOrWhiteSpace then
            Error "stable XTrace requires a non-empty Host id for every semantic message"
        elif (messageIds |> Set.ofList |> Set.count) <> List.length messageIds then
            Error "stable XTrace requires unique Host message ids"
        else
            Ok()

    let private legacySemanticMatch (work: PartCaptureWork) (part: XTracePartRef) =
        let kind, toolName, body = partShape work.Source.Part

        part.ProviderRun = work.Message.ProviderRun
        && part.Kind = kind
        && part.ToolName = toolName
        && part.ToolCallId = work.Source.ToolCallId
        && part.HostToolPartId = work.Source.HostToolPartId
        && BlobDigest.value part.TextDigest = HostDigest.sha256Hex body

    let private stablePartAlreadyCaptured (existing: XTracePartRef list) (work: PartCaptureWork) =
        let exactPhysicalProvenance =
            match work.Source.HostPartId with
            | Some _ -> existing |> List.exists (fun part -> part.Provenance = work.Provenance)
            | None -> false

        let exactPhysicalToolPart =
            match work.Source.HostToolPartId with
            | Some hostToolPartId ->
                existing
                |> List.exists (fun part ->
                    part.ProviderRun = work.Message.ProviderRun
                    && part.HostToolPartId = Some hostToolPartId
                    && part.ToolCallId = work.Source.ToolCallId)
            | None -> false

        let compatibleLegacyPosition =
            work.LegacyStableProvenance
            |> Option.exists (fun provenance ->
                existing
                |> List.exists (fun part -> part.Provenance = provenance && legacySemanticMatch work part))

        exactPhysicalProvenance || exactPhysicalToolPart || compatibleLegacyPosition

    /// One stable Host part: physical part identity wins. Legacy positional
    /// provenance is accepted only while the semantic payload at that slot is
    /// still the same physical observation.
    let private appendStablePartIfAbsent
        (durable: AgentJournal)
        (sessionId: SessionId)
        (existing: XTracePartRef list)
        (nextCursor: unit -> int64)
        (work: PartCaptureWork)
        : Task<Result<unit, string>> =
        taskResult {
            if stablePartAlreadyCaptured existing work then
                return ()
            else
                let cursor = nextCursor ()
                let kind, toolName, body = partShape work.Source.Part
                let! blob = durable.WriteBlob body |> mapBlobWriteError "XTrace part blob write failed"

                do!
                    CompanionFact.XTracePartAppended
                        {| SessionId = sessionId
                           CursorSequence = cursor
                           Role = work.Message.Role
                           Turn = work.TurnIndex
                           PartIndex = work.PartIndex
                           Kind = kind
                           ToolName = toolName
                           TextRef = blob.BlobRef
                           TextDigest = blob.BlobDigest
                           Provenance = work.Provenance
                           ProviderRun = work.Message.ProviderRun
                           ToolCallId = work.Source.ToolCallId
                           HostToolPartId = work.Source.HostToolPartId |}
                    |> appendFact durable sessionId work.Message.ProviderRun
                    |> TaskResultCE.ofTask
        }

    let private captureSourcesStableWithJournal
        (durable: AgentJournal)
        (sessionId: SessionId)
        (messageIds: string list)
        (messages: TraceSourceMessage list)
        : Task<Result<XTraceProjectionState option, string>> =
        taskResult {
            do! validateStableCapturePrerequisites (Some durable) sessionId messageIds messages
            let existing = xTraceOf durable sessionId
            let generation = captureGeneration durable sessionId

            let recorded = XTraceProjection.parts existing
            // DSL-MUTABLE: algorithm-scratch — next durable cursor while appending one stable capture batch
            let mutable cursor = XTraceProjection.headSequence existing

            let nextCursor () =
                cursor <- cursor + 1L
                cursor

            let works = enumerateStableCaptureWork generation messageIds messages

            for work in works do
                do! appendStablePartIfAbsent durable sessionId recorded nextCursor work

            return Some(xTraceOf durable sessionId)
        }

    let private captureSourcesStable
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (messageIds: string list)
        (messages: TraceSourceMessage list)
        : Task<Result<XTraceProjectionState option, string>> =
        task {
            match journal with
            | None -> return Ok None
            | Some durable -> return! captureSourcesStableWithJournal durable sessionId messageIds messages
        }

    /// Evidence → Decision: last-words turn/part from existing XTrace tip.
    let private lastWordsPlacement (existing: XTraceProjectionState) : int * int =
        match List.tryLast existing.Parts with
        | Some last -> last.Turn + 1, 0
        | None -> 0, 0

    let private captureLastWordsWhenAbsent
        (durable: AgentJournal)
        (sessionId: SessionId)
        (textRef: BlobRef)
        (textDigest: BlobDigest)
        (providerRun: ProviderRunIdentity)
        : Task<unit> =
        task {
            let existing = xTraceOf durable sessionId
            let generation = captureGeneration durable sessionId
            let provenance = sprintf "g:%d/last_words" generation

            let recorded =
                existing.Parts |> List.map (fun part -> part.Provenance) |> Set.ofList

            if Set.contains provenance recorded then
                return ()
            else
                let cursor = XTraceProjection.headSequence existing + 1L
                let turn, partIndex = lastWordsPlacement existing

                do!
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
        }

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
        task {
            match journal with
            | None -> return ()
            | Some durable -> do! captureLastWordsWhenAbsent durable sessionId textRef textDigest providerRun
        }

    let captureProjection
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (projection: ProviderSemanticProjection)
        : Task<XTraceProjectionState option> =
        projection.Messages
        |> List.map (fun message ->
            { Role = message.Role
              ProviderRun = None
              Parts =
                message.Parts
                |> List.map (fun part ->
                    { Part = part
                      HostPartId = None
                      ToolCallId = None
                      HostToolPartId = None }) })
        |> captureSources journal sessionId

    /// Capture an already-decoded transform view with the assistant message and
    /// Host ToolPart identities that localize a V1 tool invocation.
    let captureMessageView
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (messages: ProviderWireCapture.CapturedWireMessage list)
        : Task<XTraceProjectionState option> =
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
                      HostPartId = captured.HostPartId
                      ToolCallId = toolCallId
                      HostToolPartId = captured.HostToolPartId }) })
        |> captureSources journal sessionId

    let captureProjectionStable
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (messageIds: string list)
        (projection: ProviderSemanticProjection)
        : Task<Result<XTraceProjectionState option, string>> =
        projection.Messages
        |> List.map (fun message ->
            { Role = message.Role
              ProviderRun = None
              Parts =
                message.Parts
                |> List.map (fun part ->
                    { Part = part
                      HostPartId = None
                      ToolCallId = None
                      HostToolPartId = None }) })
        |> captureSourcesStable journal sessionId messageIds

    let captureMessageViewStable
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (messageIds: string list)
        (messages: ProviderWireCapture.CapturedWireMessage list)
        : Task<Result<XTraceProjectionState option, string>> =
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
                      HostPartId = captured.HostPartId
                      ToolCallId = toolCallId
                      HostToolPartId = captured.HostToolPartId }) })
        |> captureSourcesStable journal sessionId messageIds

    let private toCapturedWire (message: SessionMessage) : ProviderWireCapture.CapturedWireMessage =
        let hostToolByCall =
            message.ToolParts
            |> Array.fold (fun acc part -> Map.add (ToolCallId.value part.ToolCallId) part.HostToolPartId acc) Map.empty

        { Role = message.Role
          ProviderRun =
            if message.Role = "assistant" && not (String.IsNullOrWhiteSpace message.Id) then
                Some(ProviderRunIdentity.create message.Id)
            else
                None
          Parts =
            message.Parts
            |> Array.mapi (fun index part ->
                let hostPartId =
                    if index < message.PartIds.Length then message.PartIds.[index] else None

                part, hostPartId)
            |> Array.toList
            |> List.choose (fun (part, hostPartId) ->
                match part with
                | MessagePart.Text text ->
                    Some
                        { WirePart = WireText text
                          HostPartId = hostPartId
                          HostToolPartId = None }
                | MessagePart.Reasoning text ->
                    Some
                        { WirePart = WireReasoning text
                          HostPartId = hostPartId
                          HostToolPartId = None }
                | MessagePart.ToolCall(callId, name, args) ->
                    let hostToolPartId = Map.tryFind callId hostToolByCall

                    Some
                        { WirePart = WireToolCall(ToolCallId.create callId, name, args)
                          HostPartId =
                            hostPartId
                            |> Option.orElseWith (fun () ->
                                hostToolPartId
                                |> Option.map (HostToolPartId.value >> HostMessagePartId.create))
                          HostToolPartId = hostToolPartId }
                | MessagePart.ToolResult(callId, result) ->
                    let hostToolPartId = Map.tryFind callId hostToolByCall

                    Some
                        { WirePart = WireToolResult(ToolCallId.create callId, result)
                          HostPartId =
                            hostPartId
                            |> Option.orElseWith (fun () ->
                                hostToolPartId
                                |> Option.map (HostToolPartId.value >> HostMessagePartId.create))
                          HostToolPartId = hostToolPartId }
                | MessagePart.Activity _ -> None) }

    /// Synchronise XTrace with the Host snapshot (after-hook / ensureReview).
    /// Idempotent with messages.transform via the same stable or positional provenance.
    let captureSessionMessages
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (messages: SessionMessage list)
        : Task<Result<unit, string>> =
        task {
            let captured = messages |> List.map toCapturedWire
            let ids = messages |> List.map (fun message -> message.Id)

            if
                supportsStableInsertion journal sessionId
                && ids |> List.forall (fun id -> not (String.IsNullOrWhiteSpace id))
                && (Set.ofList ids |> Set.count) = List.length ids
                && List.length ids = List.length captured
            then
                match! captureMessageViewStable journal sessionId ids captured with
                | Ok _ -> return Ok()
                | Error error -> return Error error
            else
                let! _ = captureMessageView journal sessionId captured
                return Ok()
        }
