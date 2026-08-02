namespace Wanxiangshu.Next.Domain

open Wanxiangshu.Next.Domain.ProviderProjection

/// CTX-011: where the Companion has consumed to.
///
/// A message index alone is not enough. One large message may span several 200 KiB
/// chunks (CTX-003), so a chunk boundary can fall inside a turn — and a probe may
/// only use a cutoff that sits on a complete turn boundary (COMPANION-011). The two
/// positions are therefore tracked separately, and this type is the finer one.
///
/// Lives in Domain rather than Journal because both the chunker that produces it
/// and the projection that folds it need it, and the chunker is the pure function
/// the projection's rule is about.
type SemanticCursor = { TurnIndex: int; PartIndex: int }

/// One chunk of Blogger delta: what to render, and where committing it leaves the
/// Companion (CTX-011, CTX-013).
type BloggerDeltaChunk =
    {
        Items: BloggerDeltaItem list
        Toml: string
        /// Where `IngestCursor` lands after this chunk commits.
        NextCursor: SemanticCursor
        /// Where `CoverableTurnCutoffExclusive` lands. Equal to the previous value
        /// unless this chunk finished one or more whole turns.
        NextCoverableTurnCutoffExclusive: int
    }

[<RequireQualifiedAccess>]
module BloggerDelta =

    let private renderChunk (items: BloggerDeltaItem list) =
        BloggerToml.renderWith [ CompanionPrompt.NormalInstruction ] items

    /// CTX-003: the input contract. Not an estimate and not compared to any model's
    /// window — it only bounds one rendered TOML chunk.
    ///
    /// Plain `let`, not `[<Literal>]`: Fable inlines a literal and emits no export,
    /// which would put the number out of reach of the layer-1 tests that pin it.
    let DeltaLimitBytes = 200 * 1024

    /// CTX-013: images carry no content into the Companion.
    ///
    /// The media type survives because "there was a PNG here" is structural, not a
    /// claim about what the image showed. The digest does not: it exists in the
    /// semantic projection for CTX-011's cutoff proof and stops at this boundary.
    let private omissionFor (mediaType: string option) =
        let isImage =
            mediaType
            |> Option.map (fun value -> value.StartsWith "image/")
            |> Option.defaultValue false

        if isImage then
            BloggerDeltaPart.ImageOmitted mediaType
        else
            BloggerDeltaPart.MediaOmitted mediaType

    let private deltaPart (part: SemanticPart) : BloggerDeltaPart =
        match part with
        | SemanticText text -> BloggerDeltaPart.TextPart text
        | SemanticReasoning text -> BloggerDeltaPart.ReasoningPart text
        | SemanticToolCall(name, args) -> BloggerDeltaPart.ToolCallPart(name, args)
        // A semantic tool result has no tool name: the wire projection drops the
        // call id and the name travels on the CALL part. Attributing one here would
        // require pairing results back to calls by position, which is exactly the
        // positional guessing VERIFY-007 removed elsewhere.
        | SemanticToolResult result -> BloggerDeltaPart.ToolResultPart("", result)
        | SemanticMedia(mediaType, _digest) -> omissionFor mediaType

    /// Flatten to addressable parts, so a cursor can point at one.
    let private addressable (messages: SemanticMessage list) =
        messages
        |> List.mapi (fun turnIndex message ->
            message.Parts
            |> List.mapi (fun partIndex part ->
                {| Turn = turnIndex
                   Part = partIndex
                   Item =
                    { Turn = turnIndex
                      Role = message.Role
                      Part = deltaPart part
                      Truncated = false } |}))
        |> List.concat

    let private isAfterOrAt (cursor: SemanticCursor) (turn: int) (part: int) =
        turn > cursor.TurnIndex || (turn = cursor.TurnIndex && part >= cursor.PartIndex)

    /// CTX-013 third level: cut one part's body so the rendered CHUNK fits.
    ///
    /// The budget applies to the rendered document, not to the item in isolation.
    /// `renderChunk` appends a trailing LF, so an item measured alone fits a budget the
    /// one-item document then exceeds by exactly that byte — a limit violation small
    /// enough to pass every eyeball review and still be a limit violation.
    ///
    /// Truncation happens on CHARACTERS of the already-normalised text, then the
    /// result is re-rendered and re-measured. Cutting the rendered UTF-8 bytes
    /// directly would split a multi-byte sequence and produce invalid TOML — the
    /// failure CTX-013 names explicitly.
    ///
    /// The tail is discarded, never carried to the next chunk. A part that is always
    /// over the limit would otherwise be re-sent forever.
    let private truncateItem (budget: int) (item: BloggerDeltaItem) : BloggerDeltaItem =
        let body =
            match item.Part with
            | BloggerDeltaPart.TextPart text
            | BloggerDeltaPart.ReasoningPart text -> Some text
            | BloggerDeltaPart.ToolCallPart(_, args) -> Some args
            | BloggerDeltaPart.ToolResultPart(_, text) -> Some text
            | BloggerDeltaPart.ImageOmitted _
            | BloggerDeltaPart.MediaOmitted _ -> None

        match body with
        // An omission marker has no body to cut. It is already a handful of bytes,
        // so a budget it cannot meet means the budget is smaller than the fixed item
        // scaffolding — a configuration error, not something to repair by emitting an
        // invalid item.
        | None -> item
        | Some text ->
            let normalized = SyntheticToml.normalizeNewlines text

            let withBody replacement =
                match item.Part with
                | BloggerDeltaPart.TextPart _ -> BloggerDeltaPart.TextPart replacement
                | BloggerDeltaPart.ReasoningPart _ -> BloggerDeltaPart.ReasoningPart replacement
                | BloggerDeltaPart.ToolCallPart(tool, _) -> BloggerDeltaPart.ToolCallPart(tool, replacement)
                | BloggerDeltaPart.ToolResultPart(tool, _) -> BloggerDeltaPart.ToolResultPart(tool, replacement)
                | other -> other

            let rendered length =
                let kept = normalized.Substring(0, length)

                { item with
                    Part = withBody (kept + "\n" + BloggerToml.TruncationMarker)
                    Truncated = true }

            let documentBytes candidate = SyntheticToml.byteCount (renderChunk [ candidate ])

            // Largest prefix length whose rendered document fits. Binary search rather
            // than byte arithmetic: the escaping and the string-form choice both
            // change the rendered size non-linearly, so only rendering can measure it.
            let mutable low = 0
            let mutable high = normalized.Length
            let mutable best = rendered 0

            while low <= high do
                let mid = low + (high - low) / 2
                let candidate = rendered mid

                if documentBytes candidate <= budget then
                    best <- candidate
                    low <- mid + 1
                else
                    high <- mid - 1

            best

    /// CTX-013: the next chunk to send, or `None` when nothing is left to consume.
    ///
    /// Three levels, in order: whole messages, then part boundaries, then a hard cut.
    /// The first level is the common case; the second only engages when one message
    /// alone exceeds the limit; the third only when one part does.
    ///
    /// `CoverableTurnCutoffExclusive` advances only across a turn that was consumed
    /// to its last part (COMPANION-011). A chunk that stops mid-turn moves the ingest
    /// cursor and nothing else, so a probe can never be built from half a turn.
    let nextChunk
        (limitBytes: int)
        (cursor: SemanticCursor)
        (previousCutoff: int)
        (messages: SemanticMessage list)
        : BloggerDeltaChunk option =
        let partsPerTurn =
            messages
            |> List.mapi (fun index message -> index, List.length message.Parts)
            |> Map.ofList

        let pending =
            addressable messages
            |> List.filter (fun entry -> isAfterOrAt cursor entry.Turn entry.Part)

        match pending with
        | [] -> None
        | first :: _ ->
            // Accumulate while the rendered document still fits. Rendering the whole
            // accumulation each time — rather than summing per-item sizes — is what
            // makes the limit exact: `renderChunk` adds the header, separators and a trailing newline
            // that a per-item sum would miss.
            let mutable accepted
                : {| Turn: int
                     Part: int
                     Item: BloggerDeltaItem |} list =
                []

            let mutable stopped = false

            for entry in pending do
                if not stopped then
                    let candidate = accepted @ [ entry ]
                    let rendered = renderChunk (candidate |> List.map (fun e -> e.Item))

                    if SyntheticToml.byteCount rendered <= limitBytes then
                        accepted <- candidate
                    else
                        stopped <- true

            let finalItems, lastTurn, lastPart =
                match accepted with
                | [] ->
                    // Level three: the first pending part does not fit alone, so it is
                    // cut. The cursor still passes the WHOLE original part.
                    let truncated = truncateItem limitBytes first.Item
                    [ truncated ], first.Turn, first.Part
                | _ ->
                    let last = List.last accepted
                    accepted |> List.map (fun e -> e.Item), last.Turn, last.Part

            let consumedWholeTurn =
                partsPerTurn
                |> Map.tryFind lastTurn
                |> Option.map (fun count -> lastPart = count - 1)
                |> Option.defaultValue false

            let nextCursor =
                if consumedWholeTurn then
                    { TurnIndex = lastTurn + 1
                      PartIndex = 0 }
                else
                    { TurnIndex = lastTurn
                      PartIndex = lastPart + 1 }

            // Every turn strictly before the cursor's turn is now complete. Derived
            // from the cursor rather than counted separately, so the two cannot drift.
            let nextCutoff = max previousCutoff nextCursor.TurnIndex

            Some
                { Items = finalItems
                  Toml = renderChunk finalItems
                  NextCursor = nextCursor
                  NextCoverableTurnCutoffExclusive = nextCutoff }
