namespace Wanxiangshu.Journal

open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

/// COMPANION-003 / HOST-005: the XTrace's durable projection.
///
/// Append-only within one lifecycle: `Opening` is captured once and never
/// overwritten, `Parts` grow strictly by cursor, `Terminal` is captured once.
/// `RecordCoverage` (what Y has consumed) advances via `BlogEntryCommitted`,
/// NOT here — this module holds the record itself, the Blog projection holds
/// the ingest position, so the two can be validated against each other in the
/// fold without a second copy.
///
/// Bodies live in blobs (PERSIST-007): the journal line carries cursor, kind,
/// role, digest and reference; the text is resolved at the read boundary, the
/// same way `XWire.readFrames` resolves `BlogFrame.TextRef`.
type XTraceProjectionState =
    {
        /// The opening task, inline: it is the first task prompt, bounded and
        /// human-sized, and the fold must be able to materialise the LWR's
        /// opening section without a second read step.
        Opening: OpeningPromptRaw option
        /// Semantic parts, strictly ordered by cursor. `Kind` is one of
        /// text / reasoning / tool_call / tool_result / media.
        Parts: XTracePartRef list
        /// The terminal output blob reference. Text resolved at read boundary.
        Terminal: (BlobRef * BlobDigest) option
    }

and XTracePartRef =
    {
        Cursor: XTraceCursor
        Provenance: string
        Role: string
        /// Host semantic coordinates, kept so the writer can map a BlogEntry's
        /// SemanticCursor (turn/part) back to the XTrace cursor sequence it
        /// corresponds to. The XTrace cursor itself is independent of them.
        Turn: int
        PartIndex: int
        Kind: string
        ToolName: string option
        TextRef: BlobRef
        TextDigest: BlobDigest
    }

/// Why an XTrace line was refused (PERSIST-010).
[<RequireQualifiedAccess>]
type XTraceFoldRejection =
    /// The opening was already captured with different text. A replay must carry
    /// the SAME text; a different one means two writers disagreed about the
    /// session's opening task.
    | OpeningAlreadyCaptured
    /// A part arrived with a cursor not after the projection's head. Either a
    /// duplicate (same cursor) or a reordering (older cursor after newer).
    | CursorNotAfterHead of expected: int64 * actual: int64
    /// Terminal was already captured. Idempotent replay carries the same digest;
    /// a different one is a second terminal for one lifecycle.
    | TerminalAlreadyCaptured

module XTraceProjection =

    let empty =
        { Opening = None
          Parts = []
          Terminal = None }

    let headSequence (state: XTraceProjectionState) : int64 =
        match List.tryLast state.Parts with
        | Some part -> part.Cursor.Sequence
        | None -> 0L

    /// COMPANION-003: capture the opening task verbatim. Idempotent: replaying
    /// the same text changes nothing; a DIFFERENT text is refused (PERSIST-010).
    let applyOpening
        (assignment: string)
        (requirements: string list)
        (state: XTraceProjectionState)
        : Result<XTraceProjectionState, XTraceFoldRejection> =
        match state.Opening with
        | Some existing when
            existing.AssignmentText = assignment
            && existing.AuthoritativeRequirements = requirements
            ->
            Ok state
        | Some _ -> Error XTraceFoldRejection.OpeningAlreadyCaptured
        | None ->
            Ok
                { state with
                    Opening =
                        Some
                            { AssignmentText = assignment
                              AuthoritativeRequirements = requirements } }

    /// COMPANION-003 / HOST-005: append one semantic part reference. Strictly
    /// monotonic; a duplicate cursor or a retreat is refused (PERSIST-010).
    let applyPart
        (cursorSequence: int64)
        (role: string)
        (provenance: string)
        (turn: int)
        (partIndex: int)
        (kind: string)
        (toolName: string option)
        (textRef: BlobRef)
        (textDigest: BlobDigest)
        (state: XTraceProjectionState)
        : Result<XTraceProjectionState, XTraceFoldRejection> =
        let head = headSequence state

        if cursorSequence <= head then
            Error(XTraceFoldRejection.CursorNotAfterHead(head, cursorSequence))
        else
            Ok
                { state with
                    Parts =
                        state.Parts
                        @ [ { Cursor = { Sequence = cursorSequence }
                              Provenance = provenance
                              Role = role
                              Turn = turn
                              PartIndex = partIndex
                              Kind = kind
                              ToolName = toolName
                              TextRef = textRef
                              TextDigest = textDigest } ] }

    /// COMPANION-003: capture the terminal output reference. Idempotent: replaying
    /// the same ref changes nothing; a different one is refused.
    let applyTerminal
        (textRef: BlobRef)
        (textDigest: BlobDigest)
        (state: XTraceProjectionState)
        : Result<XTraceProjectionState, XTraceFoldRejection> =
        match state.Terminal with
        | Some(existingRef, existingDigest) when existingRef = textRef && existingDigest = textDigest -> Ok state
        | Some _ -> Error XTraceFoldRejection.TerminalAlreadyCaptured
        | None ->
            Ok
                { state with
                    Terminal = Some(textRef, textDigest) }

    /// Provenance generation: `g:N/...` after HOST-006 reanchor; legacy `turn:N/part:M` → 0.
    let provenanceGeneration (provenance: string) : int =
        if provenance.StartsWith("g:") then
            let rest = provenance.Substring(2)
            let slash = rest.IndexOf('/')
            let token = if slash < 0 then rest else rest.Substring(0, slash)

            match System.Int32.TryParse token with
            | true, n when n >= 0 -> n
            | _ -> 0
        else
            0

    /// Host turn indices restart per reanchor generation; XTrace Sequence does not.
    /// Turn/Part labels are only comparable within one generation.
    let currentGenerationParts (parts: XTracePartRef list) : XTracePartRef list =
        match parts with
        | [] -> []
        | _ ->
            let maxGen =
                parts |> List.map (fun part -> provenanceGeneration part.Provenance) |> List.max

            parts |> List.filter (fun part -> provenanceGeneration part.Provenance = maxGen)

    /// Map an ingest cursor (sequence of the last COVERED part) to the semantic
    /// cursor of the first UNCOVERED part. `>` not `>=`: the coverage sequence
    /// names a part already consumed, so the delta starts strictly after it.
    /// A sequence past the head means "nothing left" (one turn past the last part).
    ///
    /// After HOST-006 reanchor, Host renumbering voids old turn indices. Resolve the
    /// cursor against the current generation only so nextChunk / lastCoveredSequence
    /// share one numbering. Pre-reanchor sequences still advance coverage via
    /// absolute Sequence, but their Turn labels are never mixed with post-reanchor ones.
    let semanticCursorFor (sequence: int64) (state: XTraceProjectionState) : SemanticCursor =
        let searchable =
            let current = currentGenerationParts state.Parts

            if List.isEmpty current then state.Parts else current

        match searchable |> List.tryFind (fun part -> part.Cursor.Sequence > sequence) with
        | Some part ->
            { TurnIndex = part.Turn
              PartIndex = part.PartIndex }
        | None ->
            match List.tryLast searchable with
            | Some last ->
                { TurnIndex = last.Turn + 1
                  PartIndex = 0 }
            | None -> { TurnIndex = 0; PartIndex = 0 }
