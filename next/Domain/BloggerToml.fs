namespace Wanxiangshu.Next.Domain

/// CTX-013: what a Blogger delta part is, and how it renders as TOML.
///
/// The string rules and the document layout are NOT here — they belong to
/// `SyntheticToml`, which ARCH-010 makes the single owner of both. What lives here is
/// the part of the payload that is genuinely Blogger's: which parts exist, the fixed
/// key order, and the truncation marker. That split is the clause's 「不引入统一
/// envelope」 read literally: shared syntax, local schema.
///
/// TOML is a one-way LLM-facing representation. The canonical digest is always taken
/// from `ProviderSemanticProjection`, never from this text. There is no parser.
[<RequireQualifiedAccess>]
type BloggerDeltaPart =
    | TextPart of text: string
    | ReasoningPart of text: string
    | ToolCallPart of tool: string * canonicalArgs: string
    | ToolResultPart of tool: string * text: string
    /// CTX-013: the Companion has no vision. The placeholder says "an image was
    /// here" and nothing about its content — no OCR, no caption, no digest.
    ///
    /// The digest exists in `ProviderSemanticProjection.SemanticMedia` because
    /// CTX-011's cutoff proof needs two prefixes containing different images to
    /// compare unequal. It stops here: a digest in the delta would be a fact about
    /// the image that the Companion could carry into B.
    | ImageOmitted of mediaType: string option
    | MediaOmitted of mediaType: string option

/// One rendered `[[item]]`: which turn and role it came from, plus the part.
type BloggerDeltaItem =
    {
        Turn: int
        Role: string
        Part: BloggerDeltaPart
        /// Set when CTX-013's third-level hard truncation cut this part's body.
        Truncated: bool
    }

[<RequireQualifiedAccess>]
module BloggerToml =

    /// CTX-013's fixed marker. A caller must not compose its own: the text is part
    /// of what the Companion reads, and a second spelling would make one truncation
    /// look different from another for no reason.
    let TruncationMarker = "[… content truncated by Companion delta 200 KiB limit …]"

    let private kindOf (part: BloggerDeltaPart) =
        match part with
        | BloggerDeltaPart.TextPart _ -> "text"
        | BloggerDeltaPart.ReasoningPart _ -> "reasoning"
        | BloggerDeltaPart.ToolCallPart _ -> "tool_call"
        | BloggerDeltaPart.ToolResultPart _ -> "tool_result"
        | BloggerDeltaPart.ImageOmitted _ -> "image_omitted"
        | BloggerDeltaPart.MediaOmitted _ -> "media_omitted"

    /// CTX-013's fixed key order: turn, role, kind, tool, media_type, text, args,
    /// truncated. Absent optional fields are omitted rather than emitted as empty —
    /// "no tool" and "a tool named empty string" are different claims.
    ///
    /// Built as an ordered list rather than a record so the order is the code's
    /// subject. A record plus a serializer would put the order in the serializer,
    /// where a library upgrade can change it.
    let renderItem (item: BloggerDeltaItem) : string =
        let field name value = SyntheticToml.field name value
        let value text = SyntheticToml.renderString text

        let head =
            [ field "turn" (string item.Turn)
              field "role" (value item.Role)
              field "kind" (value (kindOf item.Part)) ]

        let body =
            match item.Part with
            | BloggerDeltaPart.TextPart text
            | BloggerDeltaPart.ReasoningPart text -> [ field "text" (value text) ]
            | BloggerDeltaPart.ToolCallPart(tool, args) ->
                // `args` is already canonical: it is the value the Host codec put
                // into the wire projection, with keys recursively sorted. Re-sorting
                // here would be a second canonicaliser that could disagree with the
                // one the seal digest used.
                [ field "tool" (value tool); field "args" (value args) ]
            | BloggerDeltaPart.ToolResultPart(tool, text) -> [ field "tool" (value tool); field "text" (value text) ]
            | BloggerDeltaPart.ImageOmitted mediaType
            | BloggerDeltaPart.MediaOmitted mediaType ->
                match mediaType with
                | Some mediaValue -> [ field "media_type" (value mediaValue) ]
                | None -> []

        let truncated = if item.Truncated then [ field "truncated" "true" ] else []

        // media_type precedes text/args in the fixed order, and only one of the two
        // groups is ever non-empty, so appending is enough to honour it.
        SyntheticToml.tableArrayEntry "item" (head @ body @ truncated)

    /// CTX-013: the whole document, optionally carrying an instruction header.
    ///
    /// `instructions` is normally empty — CTX-013 does not require every chunk to
    /// repeat a header, and a data-only delta must not pay for one. When a caller does
    /// supply instructions, `SyntheticToml.document` places them first and inserts the
    /// single blank line, so instruction-first and 「data body 不输出 comment」 are
    /// structural here rather than something this module re-implements.
    ///
    /// The empty document is `""`, not `"\n"`: an empty chunk is never sent, so a lone
    /// newline would only ever be a byte-count discrepancy.
    let renderWith (instructions: string list) (items: BloggerDeltaItem list) : string =
        SyntheticToml.document instructions (items |> List.map renderItem)

    /// The data-only document for callers whose instruction is carried separately.
    let render (items: BloggerDeltaItem list) : string = renderWith [] items
