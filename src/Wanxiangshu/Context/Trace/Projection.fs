// primary_owner: semantic-trace — SemanticTrace.SurfaceSurface — KEEP — XTrace projection surface
namespace Wanxiangshu.Context.Trace

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
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// COMPANION-003 / HOST-005: the XTrace's durable projection.
///
/// Append-only within one lifecycle: `Opening` is captured once and never
/// overwritten, `Parts` grow strictly by cursor, terminal occurrences grow by
/// ProviderRun as a reusable managed session completes successive work units.
/// `RecordCoverage` (what Y has consumed) advances via `BlogObservationCommitted`,
/// NOT here — this module holds the record itself, the Blog projection holds
/// the ingest position, so the two can be validated against each other in the
/// fold without a second copy.
///
/// Bodies live in blobs (PERSIST-007): the journal line carries cursor, kind,
/// role, digest and reference; the text is resolved at the read boundary, the
/// same way `XWire.readFrames` resolves `BlogFrame.TextRef`.
type XTraceProjectionState =
    {
        /// OpeningMaterial InitialCharge, inline: first task prompt, bounded and
        /// human-sized; fold materialises the LWR Opening section without a
        /// second read. BlindPlan interval end = WorkRecordStart (COMPANION-014).
        Opening: OpeningMaterial option
        /// Semantic parts. Stored newest-first so replay cons is O(1);
        /// `XTraceProjection.parts` restores oldest-first cursor order.
        /// `Kind` is one of text / reasoning / tool_call / tool_result / media.
        Parts: XTracePartRef list
        /// Terminal occurrences, newest first. Reusable managed sessions may
        /// complete many work units; each occurrence keeps the ProviderRun and
        /// the XTrace part frontier at which that completion happened so a
        /// bounded WorkRecord can project only its own formal statement.
        Terminals: XTraceTerminalRef list
    }

/// DSL-state-combination: domain — optional tool/provider/host identities are
/// provenance facets of one immutable trace part, not independent execution
/// stages or mutable continuation state.
and XTracePartRef =
    {
        Cursor: XTraceCursor
        Provenance: string
        /// Parsed once at fold from Provenance (`g:N/...`; legacy → 0).
        Generation: int
        Role: string
        /// Host semantic coordinates, kept so the writer can map a BlogEntry's
        /// SemanticCursor (turn/part) back to the XTrace cursor sequence it
        /// corresponds to. The XTrace cursor itself is independent of them.
        Turn: int
        PartIndex: int
        Kind: string
        ToolName: string option
        ProviderRun: ProviderRunIdentity option
        ToolCallId: ToolCallId option
        HostToolPartId: HostToolPartId option
        TextRef: BlobRef
        TextDigest: BlobDigest
    }

and XTraceTerminalRef =
    { TextRef: BlobRef
      TextDigest: BlobDigest
      ProviderRun: ProviderRunIdentity
      Frontier: XTraceCursor }

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
    /// This ProviderRun already captured a different terminal. Idempotent replay
    /// carries the same digest; another ProviderRun owns another occurrence.
    | TerminalAlreadyCaptured

module XTraceProjection =

    let empty =
        { Opening = None
          Parts = []
          Terminals = [] }

    /// Oldest-first cursor order. The stored field is newest-first.
    let parts (state: XTraceProjectionState) : XTracePartRef list = List.rev state.Parts

    let headSequence (state: XTraceProjectionState) : int64 =
        match state.Parts with
        | part :: _ -> part.Cursor.Sequence
        | [] -> 0L

    /// One-past last part (XTrace.head semantics). Empty projection is 0.
    /// Distinct from headSequence, which is the last assigned part (or 0).
    let head (state: XTraceProjectionState) : int64 =
        match state.Parts with
        | part :: _ -> part.Cursor.Sequence + 1L
        | [] -> 0L

    let latestTerminal (state: XTraceProjectionState) : XTraceTerminalRef option = List.tryHead state.Terminals

    let terminalForProviderRun (providerRun: ProviderRunIdentity) (state: XTraceProjectionState) =
        state.Terminals
        |> List.tryFind (fun terminal -> terminal.ProviderRun = providerRun)

    let private parseGeneration (token: string) =
        match System.Int32.TryParse token with
        | true, n when n >= 0 -> n
        | _ -> 0

    let rec private collectCurrentGeneration maxGen acc remaining =
        match remaining with
        | [] -> List.rev acc
        | part :: tail when part.Generation > maxGen -> collectCurrentGeneration part.Generation [ part ] tail
        | part :: tail when part.Generation = maxGen -> collectCurrentGeneration maxGen (part :: acc) tail
        | _ :: tail -> collectCurrentGeneration maxGen acc tail

    let private cursorAfterSearch searchable =
        match List.tryLast searchable with
        | Some last ->
            { TurnIndex = last.Turn + 1
              PartIndex = 0 }
        | None -> { TurnIndex = 0; PartIndex = 0 }

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
                              AuthoritativeRequirements = requirements
                              ConstitutiveBody = "" } }

    /// Provenance generation: `g:N/...` after HOST-006 reanchor; legacy `turn:N/part:M` → 0.
    let provenanceGeneration (provenance: string) : int =
        if provenance.StartsWith("g:") then
            let rest = provenance.Substring(2)
            let slash = rest.IndexOf('/')
            let token = if slash < 0 then rest else rest.Substring(0, slash)
            parseGeneration token
        else
            0

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
        (providerRun: ProviderRunIdentity option)
        (toolCallId: ToolCallId option)
        (hostToolPartId: HostToolPartId option)
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
                        { Cursor = { Sequence = cursorSequence }
                          Provenance = provenance
                          Generation = provenanceGeneration provenance
                          Role = role
                          Turn = turn
                          PartIndex = partIndex
                          Kind = kind
                          ToolName = toolName
                          ProviderRun = providerRun
                          ToolCallId = toolCallId
                          HostToolPartId = hostToolPartId
                          TextRef = textRef
                          TextDigest = textDigest }
                        :: state.Parts }

    /// COMPANION-003 / EXEC-009: capture one terminal occurrence. Idempotency is
    /// scoped to ProviderRun, not terminal bytes: two reused work units may
    /// legitimately return identical prose and still need distinct bounded
    /// completion evidence.
    let applyTerminal
        (textRef: BlobRef)
        (textDigest: BlobDigest)
        (providerRun: ProviderRunIdentity)
        (state: XTraceProjectionState)
        : Result<XTraceProjectionState, XTraceFoldRejection> =
        match terminalForProviderRun providerRun state with
        | Some existing when existing.TextRef = textRef && existing.TextDigest = textDigest -> Ok state
        | Some _ -> Error XTraceFoldRejection.TerminalAlreadyCaptured
        | None ->
            Ok
                { state with
                    Terminals =
                        { TextRef = textRef
                          TextDigest = textDigest
                          ProviderRun = providerRun
                          Frontier = { Sequence = head state } }
                        :: state.Terminals }

    /// Host turn indices restart per reanchor generation; XTrace Sequence does not.
    /// Turn/Part labels are only comparable within one generation.
    let currentGenerationParts (parts: XTracePartRef list) : XTracePartRef list =
        match parts with
        | [] -> []
        | first :: rest -> collectCurrentGeneration first.Generation [ first ] rest

    /// Stable Host message identity encoded by the capture provenance.
    /// Positional legacy provenance intentionally returns None: callers may use a
    /// bounded legacy fallback, but new prefix proof/writeback must not mistake a
    /// request-local array index for historical identity.
    let private nonBlankHostMessageId (messageId: string) : string option =
        if System.String.IsNullOrWhiteSpace messageId then
            None
        else
            Some messageId

    let private hostMessageIdFrom (part: XTracePartRef) (idStart: int) : string option =
        let slash = part.Provenance.IndexOf('/', idStart)
        let idEnd = if slash < 0 then part.Provenance.Length else slash
        let length = idEnd - idStart

        if length <= 0 then
            None
        else
            part.Provenance.Substring(idStart, length) |> nonBlankHostMessageId

    let tryHostMessageId (part: XTracePartRef) : string option =
        let marker = "/msg:"
        let start = part.Provenance.IndexOf(marker, System.StringComparison.Ordinal)

        if start < 0 then
            None
        else
            hostMessageIdFrom part (start + marker.Length)

    /// Canonical semantic turn occupied by one physical Host message in the
    /// current reanchor generation.
    let tryTurnOfHostMessageId (messageId: string) (state: XTraceProjectionState) : int option =
        state
        |> parts
        |> currentGenerationParts
        |> List.tryPick (fun part ->
            match tryHostMessageId part with
            | Some stable when stable = messageId -> Some part.Turn
            | _ -> None)

    /// The first stable user message in the current Host generation is the raw
    /// session Opening. Same-session prefix replacement must never delete it;
    /// WORK-RECORD-007 renders frozen-prefix records with includeOpening=false.
    let tryOpeningHostMessageId (state: XTraceProjectionState) : string option =
        state
        |> parts
        |> currentGenerationParts
        |> List.tryPick (fun part ->
            if part.Role.Equals("user", System.StringComparison.OrdinalIgnoreCase) then
                tryHostMessageId part
            else
                None)

    /// Stable physical messages whose canonical X semantic turn is strictly
    /// before cutoff. Distinct preserves encounter order, so prefix writeback can
    /// delete exactly the historical rows proved by XTrace without moving an
    /// unrelated request-local presentation row across the boundary.
    let hostMessageIdsBeforeTurn (cutoffExclusive: int) (state: XTraceProjectionState) : string list =
        state
        |> parts
        |> currentGenerationParts
        |> List.filter (fun part -> part.Turn < cutoffExclusive)
        |> List.choose tryHostMessageId
        |> List.distinct

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
            let ordered = parts state
            let current = currentGenerationParts ordered

            if List.isEmpty current then ordered else current

        match searchable |> List.tryFind (fun part -> part.Cursor.Sequence > sequence) with
        | Some part ->
            { TurnIndex = part.Turn
              PartIndex = part.PartIndex }
        | None -> cursorAfterSearch searchable
