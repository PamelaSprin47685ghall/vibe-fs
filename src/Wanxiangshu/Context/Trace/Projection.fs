namespace Wanxiangshu.Context.Trace

open Fable.Core
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation

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
/// The journal supplies verified payload bytes at fold. Trace owns the material;
/// this projection retains semantic coordinates and durable references.
type XTraceProjectionState =
    private
        {
            /// InitialCharge, inline: first task prompt, bounded and
            /// human-sized; fold materialises the LWR Opening section without a
            /// second read. Opening interval end = WorkRecordStart (COMPANION-014).
            Opening: XTraceOpeningEvidence option
            OpeningCapturedBy: EventId list
            OpeningShortcut: EventId option
            /// Semantic parts. Stored newest-first so replay cons is O(1);
            /// `XTraceProjection.parts` restores oldest-first cursor order.
            /// `Kind` is one of text / reasoning / tool_call / tool_result / media.
            Parts: XTracePartRef list
            PartWitnesses: EventId list
            PrefixShortcut: EventId option
            /// Terminal occurrences, newest first. Reusable managed sessions may
            /// complete many work units; each occurrence keeps the ProviderRun and
            /// the XTrace part frontier at which that completion happened so a
            /// bounded WorkRecord can project only its own formal statement.
            Terminals: XTraceTerminalRef list
            TerminalsByRun: Map<ProviderRunIdentity, XTraceTerminalRef list>
            /// Composition-layer bridge to the underlying wanxiangli/trace instance.
            Bridge: WanxiangliTrace.ProjectionBridge
        }

/// DSL-state-combination: domain — optional tool/provider/host identities are
/// provenance facets of one immutable trace part, not independent execution
/// stages or mutable continuation state.
and internal XTracePartRef =
    {
        CapturedBy: EventId option
        PrefixWitnesses: EventId list
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

and internal XTraceTerminalRef =
    { CapturedBy: EventId option
      HasCausalRange: bool
      HasCausalOpening: bool
      TextRef: BlobRef
      TextDigest: BlobDigest
      ProviderRun: ProviderRunIdentity
      Frontier: XTraceCursor }

/// Stable query result for one semantic part. This is a copied view, never the
/// projection's append storage, so callers cannot mutate or retain owner state.
type XTraceSemanticPartView =
    { Cursor: XTraceCursor
      Provenance: string
      Generation: int
      Role: string
      Turn: int
      PartIndex: int
      Kind: string
      ToolName: string option
      ProviderRun: ProviderRunIdentity option
      ToolCallId: ToolCallId option
      HostToolPartId: HostToolPartId option
      TextRef: BlobRef
      TextDigest: BlobDigest }

type XTraceTerminalEvidence =
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
    | MaterialRejected of reason: string

module XTraceProjection =

    let private emptyWithIdentity traceId =
        { Opening = None
          OpeningCapturedBy = []
          OpeningShortcut = None
          Parts = []
          PartWitnesses = []
          PrefixShortcut = None
          Terminals = []
          TerminalsByRun = Map.empty
          Bridge = WanxiangliTrace.emptyBridge traceId }

    let empty = emptyWithIdentity (WanxiangliTrace.traceIdentity "projection")

    let emptyForSession sessionId =
        emptyWithIdentity (WanxiangliTrace.traceIdentity (SessionId.value sessionId))

    /// Oldest-first cursor order. The stored field is newest-first.
    let internal parts (state: XTraceProjectionState) : XTracePartRef list = List.rev state.Parts

    let internal partCount (state: XTraceProjectionState) = List.length state.Parts

    let internal terminalCount (state: XTraceProjectionState) = List.length state.Terminals

    let internal openingCaptured (state: XTraceProjectionState) = Option.isSome state.Opening

    let private semanticPartView (part: XTracePartRef) : XTraceSemanticPartView =
        { Cursor = part.Cursor
          Provenance = part.Provenance
          Generation = part.Generation
          Role = part.Role
          Turn = part.Turn
          PartIndex = part.PartIndex
          Kind = part.Kind
          ToolName = part.ToolName
          ProviderRun = part.ProviderRun
          ToolCallId = part.ToolCallId
          HostToolPartId = part.HostToolPartId
          TextRef = part.TextRef
          TextDigest = part.TextDigest }

    let private terminalEvidence (terminal: XTraceTerminalRef) : XTraceTerminalEvidence =
        { TextRef = terminal.TextRef
          TextDigest = terminal.TextDigest
          ProviderRun = terminal.ProviderRun
          Frontier = terminal.Frontier }

    /// Opening proof copied out of the opaque projection.
    let openingEvidence (state: XTraceProjectionState) : XTraceOpeningEvidence option =
        state.Opening
        |> Option.map (fun opening ->
            { AssignmentText = opening.AssignmentText
              AuthoritativeRequirements = opening.AuthoritativeRequirements
              ConstitutiveBody = opening.ConstitutiveBody })

    let hasOpening (state: XTraceProjectionState) = Option.isSome state.Opening

    let hasSemanticParts (state: XTraceProjectionState option) =
        state |> Option.exists (fun trace -> not (List.isEmpty trace.Parts))

    /// Stable oldest-first semantic query view across the whole lifecycle.
    let orderedSemanticParts (state: XTraceProjectionState) : XTraceSemanticPartView list =
        parts state |> List.map semanticPartView

    let internal headSequence (state: XTraceProjectionState) : int64 =
        match state.Parts with
        | part :: _ -> XTraceCursor.sequence part.Cursor
        | [] -> 0L

    /// Latest assigned part cursor, without conflating an empty trace with
    /// sequence zero.
    let latestPartCursor (state: XTraceProjectionState) : XTraceCursor option =
        state.Parts |> List.tryHead |> Option.map (fun part -> part.Cursor)

    /// One-past last part (XTrace.head semantics). Empty projection is 0.
    /// Distinct from headSequence, which is the last assigned part (or 0).
    let internal head (state: XTraceProjectionState) : int64 =
        match state.Parts with
        | part :: _ -> XTraceCursor.sequence part.Cursor + 1L
        | [] -> 0L

    /// One-past the latest part as an opaque trace cursor.
    let headCursor (state: XTraceProjectionState) : XTraceCursor = XTraceCursor.create (head state)

    let rangeFrom (startInclusive: XTraceCursor) (state: XTraceProjectionState) : XTraceRange =
        let traceHead = headCursor state

        let effectiveStart =
            if XTraceCursor.isAfter startInclusive traceHead then
                traceHead
            else
                startInclusive

        XTraceRange.create effectiveStart traceHead

    let slice (range: XTraceRange) (state: XTraceProjectionState) : XTraceSemanticPartView list =
        WanxiangliTrace.verifyAndReadIntervalOnBridge range state.Bridge |> ignore

        parts state
        |> List.filter (fun part -> XTraceRange.contains part.Cursor range)
        |> List.map semanticPartView

    let frontierAfter (part: XTraceSemanticPartView) : XTraceCursor = XTraceCursor.nextCursor part.Cursor

    let rangeOfPart (part: XTraceSemanticPartView) : XTraceRange =
        XTraceRange.create part.Cursor (frontierAfter part)

    let partKinds (state: XTraceProjectionState) : string list =
        parts state |> List.map (fun part -> part.Kind)

    let internal terminalForProviderRun (providerRun: ProviderRunIdentity) (state: XTraceProjectionState) =
        let candidates =
            state.TerminalsByRun |> Map.tryFind providerRun |> Option.defaultValue []

        candidates
        |> List.tryHead
        |> Option.filter (fun first ->
            candidates
            |> List.forall (fun candidate ->
                candidate.HasCausalRange
                && candidate.Frontier = first.Frontier
                && candidate.TextRef = first.TextRef
                && candidate.TextDigest = first.TextDigest))

    let internal latestTerminal (state: XTraceProjectionState) : XTraceTerminalRef option =
        state.Terminals
        |> List.tryHead
        |> Option.bind (fun terminal -> terminalForProviderRun terminal.ProviderRun state)

    let latestTerminalEvidence (state: XTraceProjectionState) : XTraceTerminalEvidence option =
        latestTerminal state |> Option.map terminalEvidence

    let terminalEvidenceForProviderRun
        (providerRun: ProviderRunIdentity)
        (state: XTraceProjectionState)
        : XTraceTerminalEvidence option =
        terminalForProviderRun providerRun state |> Option.map terminalEvidence

    let completionRangeForProviderRun providerRun (state: XTraceProjectionState) =
        let candidates =
            state.TerminalsByRun |> Map.tryFind providerRun |> Option.defaultValue []

        let boundary =
            candidates
            |> List.map (fun terminal -> terminal.Frontier)
            |> List.sortByDescending XTraceCursor.sequence
            |> List.tryHead

        boundary
        |> Option.map (fun through ->
            terminalEvidenceForProviderRun providerRun state
            |> Option.filter (fun _ -> candidates |> List.forall (fun terminal -> terminal.HasCausalOpening))
            |> Option.map Ok
            |> Option.defaultValue (Error through))

    let private parseGeneration (token: string) =
        match System.Int32.TryParse token with
        | true, n when n >= 0 -> n
        | _ -> 0

    let rec private collectCurrentGeneration
        (maxGen: int)
        (acc: XTracePartRef list)
        (remaining: XTracePartRef list)
        : XTracePartRef list =
        match remaining with
        | [] -> List.rev acc
        | part :: tail when part.Generation > maxGen -> collectCurrentGeneration part.Generation [ part ] tail
        | part :: tail when part.Generation = maxGen -> collectCurrentGeneration maxGen (part :: acc) tail
        | _ :: tail -> collectCurrentGeneration maxGen acc tail

    let private cursorAfterSearch (searchable: XTracePartRef list) : SemanticCursor =
        match List.tryLast searchable with
        | Some last ->
            { TurnIndex = last.Turn + 1
              PartIndex = 0 }
        | None -> { TurnIndex = 0; PartIndex = 0 }

    let private appendPartRefToBridge body (part: XTracePartRef) (bridge: WanxiangliTrace.ProjectionBridge) =
        WanxiangliTrace.appendPartToBridge
            (XTraceCursor.sequence part.Cursor)
            part.Role
            part.Provenance
            part.Kind
            part.ToolName
            (part.ProviderRun |> Option.map ProviderRunIdentity.value)
            body
            bridge

    let private appendTerminalRefToBridge
        body
        (terminal: XTraceTerminalRef)
        (bridge: WanxiangliTrace.ProjectionBridge)
        =
        WanxiangliTrace.appendTerminalToBridge
            (ProviderRunIdentity.value terminal.ProviderRun)
            body
            (XTraceCursor.sequence terminal.Frontier)
            bridge

    /// COMPANION-003: capture the opening task verbatim. Idempotent: replaying
    /// the same text changes nothing; a DIFFERENT text is refused (PERSIST-010).
    let applyOpening
        capturedBy
        (assignment: string)
        (requirements: string list)
        (state: XTraceProjectionState)
        : Result<XTraceProjectionState, XTraceFoldRejection> =
        match state.Opening with
        | Some existing when
            existing.AssignmentText = assignment
            && existing.AuthoritativeRequirements = requirements
            ->
            Ok
                { state with
                    OpeningCapturedBy =
                        capturedBy
                        |> Option.map (fun eventId -> eventId :: state.OpeningCapturedBy)
                        |> Option.defaultValue state.OpeningCapturedBy }
        | Some _ -> Error XTraceFoldRejection.OpeningAlreadyCaptured
        | None ->
            let nextBridge =
                WanxiangliTrace.appendOpeningToBridge assignment requirements state.Bridge

            Ok
                { state with
                    OpeningCapturedBy = capturedBy |> Option.toList
                    Opening =
                        Some
                            { AssignmentText = assignment
                              AuthoritativeRequirements = requirements
                              ConstitutiveBody = "" }
                    Bridge = nextBridge }

    /// Provenance generation: `g:N/...` after HOST-006 reanchor; legacy `turn:N/part:M` → 0.
    let provenanceGeneration (provenance: string) : int =
        if provenance.StartsWith("g:") then
            let rest = provenance.Substring(2)
            let slash = rest.IndexOf('/')
            let token = if slash < 0 then rest else rest.Substring(0, slash)
            parseGeneration token
        else
            0

    [<Emit("JSON.stringify($0)")>]
    let private encodeMaterialTuple (_values: string array) : string = jsNative

    [<Emit("""(() => {
        try {
            const tuple = JSON.parse($0);
            return Array.isArray(tuple) && tuple.length === 4
                && tuple.every(value => typeof value === "string")
                && ["physical", "call", "position"].includes(tuple[0])
                && ["content", "tool_call", "tool_result"].includes(tuple[3])
                && JSON.stringify(tuple) === $0 ? tuple : null;
        } catch {
            return null;
        }
    })()""")>]
    let private parseMaterialTuple (_text: string) : string array = jsNative

    let internal materialProvenance generation addressTag messageId address facet =
        let encoded = encodeMaterialTuple [| addressTag; messageId; address; facet |]

        if generation < 0 || (parseMaterialTuple encoded |> Option.ofObj).IsNone then
            invalidArg "provenance" "Material provenance requires a generation and a canonical identity tuple"

        sprintf "g:%d/material:%s" generation encoded

    let internal tryMaterialProvenance (provenance: string) =
        let prefix = sprintf "g:%d/material:" (provenanceGeneration provenance)

        if not (provenance.StartsWith(prefix, System.StringComparison.Ordinal)) then
            None
        else
            provenance.Substring(prefix.Length)
            |> parseMaterialTuple
            |> Option.ofObj
            |> Option.map (fun tuple -> tuple[0], tuple[1], tuple[2], tuple[3])

    let private appendMaterial append =
        try
            Ok(append ())
        with error ->
            Error(XTraceFoldRejection.MaterialRejected error.Message)

    let private nextShortcut proved causality previous =
        if proved then causality |> Option.map fst else previous

    let private allPartsVisible causality (state: XTraceProjectionState) =
        match causality with
        | None -> true
        | Some(_, contains) ->
            (state.PrefixShortcut |> Option.exists contains)
            || ((List.isEmpty state.Parts || not (List.isEmpty state.PartWitnesses))
                && (state.PartWitnesses |> List.forall contains))

    let private nextPartWitnesses causality state =
        match causality with
        | None -> state.PartWitnesses
        | Some(eventId, _) when allPartsVisible causality state -> [ eventId ]
        | Some(eventId, contains) -> eventId :: (state.PartWitnesses |> List.filter (contains >> not))

    /// COMPANION-003 / HOST-005: append one semantic part reference. Strictly
    /// monotonic; a duplicate cursor or a retreat is refused (PERSIST-010).
    let applyPart
        (causality: (EventId * (EventId -> bool)) option)
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
        (body: string)
        (state: XTraceProjectionState)
        : Result<XTraceProjectionState, XTraceFoldRejection> =
        let head = headSequence state

        if cursorSequence <= head then
            Error(XTraceFoldRejection.CursorNotAfterHead(head, cursorSequence))
        elif state.Parts |> List.exists (fun part -> part.Provenance = provenance) then
            Error(XTraceFoldRejection.MaterialRejected("Material identity already captured: " + provenance))
        else
            let witnesses = nextPartWitnesses causality state

            let partRef: XTracePartRef =
                { CapturedBy = causality |> Option.map fst
                  PrefixWitnesses = witnesses
                  Cursor = XTraceCursor.create cursorSequence
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

            appendMaterial (fun () -> appendPartRefToBridge body partRef state.Bridge)
            |> Result.map (fun nextBridge ->
                { state with
                    Parts = partRef :: state.Parts
                    PartWitnesses = witnesses
                    PrefixShortcut = None
                    Bridge = nextBridge })

    /// COMPANION-003 / EXEC-009: capture one terminal occurrence. Idempotency is
    /// scoped to ProviderRun, not terminal bytes: two reused work units may
    /// legitimately return identical prose and still need distinct bounded
    /// completion evidence.
    let private isCausalPart causality (part: XTracePartRef) =
        match causality, part.CapturedBy with
        | Some(_, contains), Some eventId -> contains eventId
        | None, _ -> true
        | _ -> false

    let private hasCausalOpening causality (state: XTraceProjectionState) =
        match causality with
        | Some(_, contains) ->
            (state.OpeningShortcut |> Option.exists contains)
            || (state.OpeningCapturedBy |> List.exists contains)
        | None -> state.Opening.IsSome

    let private terminalRange causality state =
        let allVisible = allPartsVisible causality state

        let part =
            if allVisible then
                List.tryHead state.Parts
            else
                state.Parts |> List.tryFind (isCausalPart causality)

        let frontier =
            part
            |> Option.map (fun part -> XTraceCursor.nextCursor part.Cursor)
            |> Option.defaultValue (XTraceCursor.create 0L)

        let contains = causality |> Option.map snd |> Option.defaultValue (fun _ -> true)

        let valid =
            allVisible
            || (part |> Option.forall (fun part -> part.PrefixWitnesses |> List.forall contains))

        frontier, valid, allVisible

    let applyTerminal
        (causality: (EventId * (EventId -> bool)) option)
        (textRef: BlobRef)
        (textDigest: BlobDigest)
        (providerRun: ProviderRunIdentity)
        (body: string)
        (state: XTraceProjectionState)
        : Result<XTraceProjectionState, XTraceFoldRejection> =
        let candidates =
            state.TerminalsByRun |> Map.tryFind providerRun |> Option.defaultValue []

        let prior =
            candidates
            |> List.tryFind (fun terminal ->
                match causality, terminal.CapturedBy with
                | Some(_, contains), Some eventId -> contains eventId
                | None, _ -> true
                | _ -> false)

        match prior with
        | Some existing when existing.TextRef = textRef && existing.TextDigest = textDigest -> Ok state
        | Some _ -> Error XTraceFoldRejection.TerminalAlreadyCaptured
        | None ->
            let frontier, valid, allVisible = terminalRange causality state

            let terminalRef: XTraceTerminalRef =
                { CapturedBy = causality |> Option.map fst
                  HasCausalRange = valid
                  HasCausalOpening = hasCausalOpening causality state
                  TextRef = textRef
                  TextDigest = textDigest
                  ProviderRun = providerRun
                  Frontier = frontier }

            (if List.isEmpty candidates && terminalRef.HasCausalRange then
                 appendMaterial (fun () -> appendTerminalRefToBridge body terminalRef state.Bridge)
             else
                 Ok state.Bridge)
            |> Result.map (fun nextBridge ->
                { state with
                    Terminals = terminalRef :: state.Terminals
                    TerminalsByRun = Map.add providerRun (terminalRef :: candidates) state.TerminalsByRun
                    PrefixShortcut = nextShortcut allVisible causality state.PrefixShortcut
                    OpeningShortcut = nextShortcut terminalRef.HasCausalOpening causality state.OpeningShortcut
                    Bridge = nextBridge })

    let traceHeadCut (state: XTraceProjectionState) : XTraceCut =
        WanxiangliTrace.headCutOfBridge state.Bridge

    let traceCutAt (cursor: XTraceCursor) (state: XTraceProjectionState) : XTraceCut =
        WanxiangliTrace.cutAtSequenceOfBridge (XTraceCursor.sequence cursor) state.Bridge

    let traceIntervalOfRange (range: XTraceRange) (state: XTraceProjectionState) : XTraceCut * XTraceCut =
        WanxiangliTrace.intervalOfRangeOnBridge range state.Bridge

    let traceIntervalAfterCoverage (coverage: RecordCoverage) (state: XTraceProjectionState) : XTraceCut * XTraceCut =
        WanxiangliTrace.intervalAfterCoverageOnBridge coverage state.Bridge

    let traceRecords (state: XTraceProjectionState) : string list =
        WanxiangliTrace.recordsOldestFirst state.Bridge

    let partText provenance (state: XTraceProjectionState) =
        try
            Ok(WanxiangliTrace.partText provenance state.Bridge)
        with error ->
            Error error.Message

    let private readTerminalText providerRun (state: XTraceProjectionState) =
        try
            Ok(WanxiangliTrace.terminalText (ProviderRunIdentity.value providerRun) state.Bridge)
        with error ->
            Error error.Message

    let terminalText providerRun (state: XTraceProjectionState) =
        terminalForProviderRun providerRun state
        |> Option.map (fun _ -> readTerminalText providerRun state)
        |> Option.defaultValue (Error "Terminal has no unique causal evidence")

    /// Host turn indices restart per reanchor generation; XTrace Sequence does not.
    /// Turn/Part labels are only comparable within one generation.
    let internal currentGenerationParts (parts: XTracePartRef list) : XTracePartRef list =
        match parts with
        | [] -> []
        | first :: rest -> collectCurrentGeneration first.Generation [ first ] rest

    let providerRunParts
        (providerRun: ProviderRunIdentity)
        (state: XTraceProjectionState)
        : XTraceSemanticPartView list =
        parts state
        |> List.filter (fun part -> part.ProviderRun = Some providerRun)
        |> List.map semanticPartView

    let toolResultParts
        (providerRun: ProviderRunIdentity)
        (toolCallId: ToolCallId)
        (state: XTraceProjectionState)
        : XTraceSemanticPartView list =
        parts state
        |> List.filter (fun part ->
            part.Kind = "tool_result"
            && part.ProviderRun = Some providerRun
            && part.ToolCallId = Some toolCallId)
        |> List.map semanticPartView

    let toolPartsForHostIdentity
        (providerRun: ProviderRunIdentity)
        (toolCallId: ToolCallId)
        (hostToolPartId: HostToolPartId)
        (state: XTraceProjectionState)
        : XTraceSemanticPartView list =
        parts state
        |> List.filter (fun part ->
            part.ProviderRun = Some providerRun
            && part.ToolCallId = Some toolCallId
            && part.HostToolPartId = Some hostToolPartId)
        |> List.map semanticPartView

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

    let private legacyHostMessageId (part: XTracePartRef) : string option =
        let marker = "/msg:"
        let start = part.Provenance.IndexOf(marker, System.StringComparison.Ordinal)

        let generation = sprintf "g:%d" part.Generation

        let legacyPrefix =
            part.Provenance.StartsWith(generation + marker, System.StringComparison.Ordinal)

        if start < 0 || not legacyPrefix then
            None
        else
            hostMessageIdFrom part (start + marker.Length)

    let internal tryHostMessageId (part: XTracePartRef) : string option =
        match tryMaterialProvenance part.Provenance with
        | Some(_, messageId, _, _) -> nonBlankHostMessageId messageId
        | None -> legacyHostMessageId part

    let internal tryPhysicalHostIdentity (part: XTracePartRef) : (string * string) option =
        match tryMaterialProvenance part.Provenance with
        | Some("physical", messageId, partId, _) -> Some(messageId, partId)
        | _ -> None

    let internal canonicalParts (ordered: XTracePartRef list) : XTracePartRef list =
        let sameMessage (left: XTracePartRef) (right: XTracePartRef) =
            left.Role = right.Role
            && left.Turn = right.Turn
            && match tryHostMessageId left, tryHostMessageId right with
               | Some leftId, Some rightId -> leftId = rightId
               | None, None -> true
               | _ -> false

        ordered
        |> List.mapFold
            (fun (previous, turn, index) part ->
                let same = previous |> Option.exists (fun prior -> sameMessage prior part)
                let nextTurn = if same then turn else turn + 1
                let nextIndex = if same then index + 1 else 0

                { part with
                    Turn = nextTurn
                    PartIndex = nextIndex },
                (Some part, nextTurn, nextIndex))
            (None, -1, -1)
        |> fst

    /// Semantic coordinates follow the durable prefix, including late facets.
    let currentGenerationSemanticParts (state: XTraceProjectionState) : XTraceSemanticPartView list =
        state
        |> parts
        |> currentGenerationParts
        |> canonicalParts
        |> List.map semanticPartView

    let lastCursorBeforeSemantic (nextCursor: SemanticCursor) (state: XTraceProjectionState) : XTraceCursor option =
        currentGenerationSemanticParts state
        |> List.tryFindBack (fun part ->
            part.Turn < nextCursor.TurnIndex
            || (part.Turn = nextCursor.TurnIndex && part.PartIndex < nextCursor.PartIndex))
        |> Option.map (fun part -> part.Cursor)

    let tryHostMessageIdAt (cursor: XTraceCursor) (state: XTraceProjectionState) : string option =
        parts state
        |> List.tryFind (fun part -> part.Cursor = cursor)
        |> Option.bind tryHostMessageId

    let hostMessageFirstCursors (state: XTraceProjectionState) : Map<string, int64> =
        parts state
        |> List.fold
            (fun cursors part ->
                match tryHostMessageId part with
                | Some messageId when not (Map.containsKey messageId cursors) ->
                    Map.add messageId (XTraceCursor.sequence part.Cursor) cursors
                | _ -> cursors)
            Map.empty

    let partsForHostMessageIds (messageIds: Set<string>) (state: XTraceProjectionState) : XTraceSemanticPartView list =
        parts state
        |> List.filter (fun part -> tryHostMessageId part |> Option.exists messageIds.Contains)
        |> List.map semanticPartView

    let tryContiguousHostRange (messageIds: Set<string>) (state: XTraceProjectionState) : XTraceRange option =
        let matched =
            parts state
            |> List.filter (fun part -> tryHostMessageId part |> Option.exists messageIds.Contains)

        let rec contiguous (previous: int64) (remaining: XTracePartRef list) =
            match remaining with
            | [] -> true
            | part :: tail when XTraceCursor.sequence part.Cursor = previous + 1L ->
                contiguous (XTraceCursor.sequence part.Cursor) tail
            | _ -> false

        let observedIds = matched |> List.choose tryHostMessageId |> Set.ofList
        let exactRequestedIds = not (Set.isEmpty messageIds) && observedIds = messageIds

        match matched, exactRequestedIds with
        | first :: tail, true when contiguous (XTraceCursor.sequence first.Cursor) tail ->
            let last = List.last matched
            Some(XTraceRange.create first.Cursor (XTraceCursor.nextCursor last.Cursor))
        | _ -> None

    /// Canonical semantic turn occupied by one physical Host message in the
    /// current reanchor generation.
    let tryTurnOfHostMessageId (messageId: string) (state: XTraceProjectionState) : int option =
        state
        |> parts
        |> currentGenerationParts
        |> canonicalParts
        |> List.tryPick (fun part ->
            match tryHostMessageId part with
            | Some stable when stable = messageId -> Some part.Turn
            | _ -> None)

    /// context-compression-028: the turn a committed tool call started in.
    ///
    /// `Bi` is the start of `Ai`'s own complete semantic turn, so the answer must be
    /// read from the CURRENT generation: a phase committed in a voided numbering
    /// cannot name a boundary the prefix may fold at. `None` therefore means "this
    /// phase has no addressable turn here", which the window treats as no desire.
    let tryTurnOfToolCallId (callId: ToolCallId) (state: XTraceProjectionState) : int option =
        state
        |> parts
        |> currentGenerationParts
        |> canonicalParts
        |> List.tryPick (fun part ->
            match part.ToolCallId with
            | Some owned when owned = callId -> Some part.Turn
            | _ -> None)

    /// context-compression-028/029: the turn of the newest captured message in the
    /// current generation — the message this request answers.
    ///
    /// A loop's later provider steps answer their own assistant/tool history, so this
    /// is NOT the trailing user message: one user message can drive many provider
    /// steps, and bounding a fold by the message that opened the loop would forbid
    /// every fold inside it. A candidate cutoff may not pass this turn, or the fold
    /// would replace the very message being answered.
    let frontierTurn (state: XTraceProjectionState) : int option =
        state
        |> parts
        |> currentGenerationParts
        |> canonicalParts
        |> List.tryLast
        |> Option.map (fun part -> part.Turn)

    /// The first stable user message in the current Host generation is the raw
    /// session Opening. Same-session prefix replacement must never delete it;
    /// work-record-007 renders frozen-prefix records with includeOpening=false.
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
        let messages =
            state
            |> parts
            |> currentGenerationParts
            |> canonicalParts
            |> List.choose (fun part -> tryHostMessageId part |> Option.map (fun id -> id, part.Turn))
            |> List.groupBy fst

        let crossesBoundary (_, occurrences) =
            let before = occurrences |> List.exists (fun (_, turn) -> turn < cutoffExclusive)
            let after = occurrences |> List.exists (fun (_, turn) -> turn >= cutoffExclusive)
            before && after

        if messages |> List.exists crossesBoundary then
            []
        else
            messages
            |> List.filter (fun (_, occurrences) ->
                occurrences |> List.forall (fun (_, turn) -> turn < cutoffExclusive))
            |> List.map fst

    /// Map an ingest cursor (sequence of the last COVERED part) to the semantic
    /// cursor of the first UNCOVERED part. `>` not `>=`: the coverage sequence
    /// names a part already consumed, so the delta starts strictly after it.
    /// A sequence past the head means "nothing left" (one turn past the last part).
    ///
    /// After HOST-006 reanchor, Host renumbering voids old turn indices. Resolve the
    /// cursor against the current generation only so nextChunk / lastCoveredSequence
    /// share one numbering. Pre-reanchor sequences still advance coverage via
    /// absolute Sequence, but their Turn labels are never mixed with post-reanchor ones.
    let internal semanticCursorFor (sequence: int64) (state: XTraceProjectionState) : SemanticCursor =
        let searchable =
            let ordered = parts state
            let current = currentGenerationParts ordered

            (if List.isEmpty current then ordered else current) |> canonicalParts

        match
            searchable
            |> List.tryFind (fun part -> XTraceCursor.sequence part.Cursor > sequence)
        with
        | Some part ->
            { TurnIndex = part.Turn
              PartIndex = part.PartIndex }
        | None -> cursorAfterSearch searchable

    let semanticCursorAfter (cursor: XTraceCursor) (state: XTraceProjectionState) : SemanticCursor =
        semanticCursorFor (XTraceCursor.sequence cursor) state

    let semanticCursorAfterCoverage (coverage: RecordCoverage) (state: XTraceProjectionState) : SemanticCursor =
        semanticCursorAfter (RecordCoverage.ingestedThrough coverage) state
