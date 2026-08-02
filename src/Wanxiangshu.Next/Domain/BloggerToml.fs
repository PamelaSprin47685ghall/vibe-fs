namespace Wanxiangshu.Next.Domain

/// CTX-013: what a Blogger delta part is, and how it renders as TOML.
///
/// The string rules and the document layout are NOT here — they belong to
/// `SyntheticToml`, which ARCH-010 makes the single owner of both. What lives here is
/// the part of the payload that is genuinely Blogger's: which parts exist and the
/// truncation marker. That split is the clause's 「不引入统一
/// envelope」 read literally: shared syntax, local schema.
///
/// TOML is a one-way LLM-facing representation. The canonical digest is always taken
/// from `ProviderSemanticProjection`, never from this text. There is no parser.
///
/// The schema is sparse (COMPANION-003 / CTX-013): the part type IS the table name,
/// so `kind` is gone; document order expresses order, so `turn` is gone; absent
/// optional fields are omitted (no empty `tool`, no `truncated = false`).
[<RequireQualifiedAccess>]
type BloggerDeltaPart =
    | TextPart of text: string
    | ReasoningPart of text: string
    | ToolCallPart of tool: string * canonicalArgs: string
    | ToolResultPart of text: string
    /// CTX-013: the Companion has no vision. The placeholder says "an image was
    /// here" and nothing about its content — no OCR, no caption, no digest.
    ///
    /// The digest exists in `ProviderSemanticProjection.SemanticMedia` because
    /// CTX-011's cutoff proof needs two prefixes containing different images to
    /// compare unequal. It stops here: a digest in the delta would be a fact about
    /// the image that the Companion could carry into B.
    | ImageOmitted of mediaType: string option
    | MediaOmitted of mediaType: string option

/// One rendered part: which role it came from (message parts only) plus the part.
type BloggerDeltaItem =
    {
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

    /// CTX-013's sparse schema. The part type is the table name, so the fixed key
    /// order is per-table:
    ///
    ///   [[message]]      role, text            (user/assistant text; role is needed
    ///                                          to tell who said it)
    ///   [[reasoning]]    text
    ///   [[tool_call]]    name, arguments
    ///   [[tool_result]]  text
    ///   [[media_omitted]] media_type (optional)
    ///
    /// Absent optional fields are omitted rather than emitted as empty — "no tool"
    /// and "a tool named empty string" are different claims.
    ///
    /// Built as an ordered list rather than a record so the order is the code's
    /// subject. A record plus a serializer would put the order in the serializer,
    /// where a library upgrade can change it.
    let renderItem (item: BloggerDeltaItem) : string =
        let field name value = SyntheticToml.field name value
        let value text = SyntheticToml.renderString text
        let truncated = if item.Truncated then [ field "truncated" "true" ] else []

        match item.Part with
        | BloggerDeltaPart.TextPart text ->
            SyntheticToml.tableArrayEntry
                "message"
                ([ field "role" (value item.Role); field "text" (value text) ] @ truncated)
        | BloggerDeltaPart.ReasoningPart text ->
            SyntheticToml.tableArrayEntry "reasoning" ([ field "text" (value text) ] @ truncated)
        | BloggerDeltaPart.ToolCallPart(tool, args) ->
            // `args` is already canonical: it is the value the Host codec put
            // into the wire projection, with keys recursively sorted. Re-sorting
            // here would be a second canonicaliser that could disagree with the
            // one the seal digest used.
            SyntheticToml.tableArrayEntry
                "tool_call"
                ([ field "name" (value tool); field "arguments" (value args) ] @ truncated)
        | BloggerDeltaPart.ToolResultPart text ->
            SyntheticToml.tableArrayEntry "tool_result" ([ field "text" (value text) ] @ truncated)
        | BloggerDeltaPart.ImageOmitted mediaType
        | BloggerDeltaPart.MediaOmitted mediaType ->
            let body =
                match mediaType with
                | Some mediaValue -> [ field "media_type" (value mediaValue) ]
                | None -> []

            SyntheticToml.tableArrayEntry "media_omitted" (body @ truncated)

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
