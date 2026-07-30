namespace Wanxiangshu.Next.Domain

open System
open System.Text

/// CTX-013: what a Blogger delta part is, and how it renders as TOML.
///
/// TOML is a one-way human-readable wire representation. The canonical digest is
/// always taken from `ProviderSemanticProjection`, never from this text, so the
/// renderer's only obligation is to be deterministic and valid — never to be
/// reversible. There is deliberately no parser here.
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

    /// CTX-013: CRLF and lone CR normalise to LF before anything else looks at the
    /// text. Without this the same logical content renders as different bytes
    /// depending on which platform produced it.
    let normalizeNewlines (text: string) =
        if isNull text then
            ""
        else
            text.Replace("\r\n", "\n").Replace("\r", "\n")

    let private escapeBasic (text: string) =
        let sb = StringBuilder()

        for ch in text do
            match ch with
            | '"' -> sb.Append "\\\"" |> ignore
            | '\\' -> sb.Append "\\\\" |> ignore
            | '\b' -> sb.Append "\\b" |> ignore
            | '\t' -> sb.Append "\\t" |> ignore
            | '\n' -> sb.Append "\\n" |> ignore
            | '\f' -> sb.Append "\\f" |> ignore
            | '\r' -> sb.Append "\\r" |> ignore
            | c when c < ' ' || c = '\u007F' -> sb.Append(sprintf "\\u%04X" (int c)) |> ignore
            | c -> sb.Append c |> ignore

        sb.ToString()

    /// Can this text sit inside `'''…'''` unchanged?
    ///
    /// A literal multi-line string processes NO escapes, which is the only way to
    /// print code and JSON verbatim. Its constraints: it cannot contain `'''`, it
    /// cannot end with `'` (that would extend the closing delimiter), and TOML
    /// forbids raw control characters other than tab and newline.
    let private literalSafe (text: string) =
        not (text.Contains "'''")
        && not (text.EndsWith "'")
        && text |> Seq.forall (fun c -> c = '\n' || c = '\t' || not (Char.IsControl c))

    /// CTX-013 string selection. Deterministic and always valid, in this order:
    ///
    ///   no newline                  → basic `"…"` with standard escapes
    ///   newline and literal-safe    → literal `'''…'''`, byte-for-byte verbatim
    ///   anything else               → basic `"…"` with everything escaped
    ///
    /// The first test is only "does it contain a newline". Every other character a
    /// body can hold — tab, NUL, DEL — has a basic-string escape, so a newline is
    /// the one thing that actually forces the multi-line form. An earlier version
    /// also excluded tab here, which pushed single-line text containing a tab into
    /// `'''…'''`: valid TOML, but it turned a one-line value into a three-line one
    /// for no reason.
    ///
    /// Multi-line BASIC strings (`"""…"""`) are deliberately never emitted, and this
    /// corrects the rule as originally drafted. A `"""` string still processes escape
    /// sequences, so a body containing a backslash — every non-trivial tool-call
    /// argument, every Windows path, every regex — would either be misread (`\n`
    /// becoming a real newline) or fail to parse at all (`\d` is not a valid TOML
    /// escape). Preferring `"""` for readability therefore buys nothing: keeping it
    /// valid requires escaping the backslashes it was supposed to avoid. `'''` is the
    /// only multi-line form that is genuinely raw.
    let renderString (raw: string) : string =
        let text = normalizeNewlines raw

        if not (text.Contains "\n") then
            "\"" + escapeBasic text + "\""
        elif literalSafe text then
            "'''\n" + text + "'''"
        else
            "\"" + escapeBasic text + "\""

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
        let field name value = name + " = " + value

        let head =
            [ field "turn" (string item.Turn)
              field "role" (renderString item.Role)
              field "kind" (renderString (kindOf item.Part)) ]

        let body =
            match item.Part with
            | BloggerDeltaPart.TextPart text
            | BloggerDeltaPart.ReasoningPart text -> [ field "text" (renderString text) ]
            | BloggerDeltaPart.ToolCallPart(tool, args) ->
                // `args` is already canonical: it is the value the Host codec put
                // into the wire projection, with keys recursively sorted. Re-sorting
                // here would be a second canonicaliser that could disagree with the
                // one the seal digest used.
                [ field "tool" (renderString tool); field "args" (renderString args) ]
            | BloggerDeltaPart.ToolResultPart(tool, text) ->
                [ field "tool" (renderString tool); field "text" (renderString text) ]
            | BloggerDeltaPart.ImageOmitted mediaType
            | BloggerDeltaPart.MediaOmitted mediaType ->
                match mediaType with
                | Some value -> [ field "media_type" (renderString value) ]
                | None -> []

        let truncated = if item.Truncated then [ field "truncated" "true" ] else []

        // media_type precedes text/args in the fixed order, and only one of the two
        // groups is ever non-empty, so appending is enough to honour it.
        String.Join("\n", "[[item]]" :: (head @ body @ truncated))

    /// CTX-013: the whole document. Exactly one trailing LF, no comments, no
    /// timestamps, no random ids, no Host message ids.
    ///
    /// The empty document is `""`, not `"\n"`: an empty chunk is never sent, so a
    /// lone newline would only ever be a byte-count discrepancy.
    let render (items: BloggerDeltaItem list) : string =
        match items with
        | [] -> ""
        | _ -> String.Join("\n\n", items |> List.map renderItem) + "\n"

    /// CTX-003: the limit is measured on the rendered UTF-8 bytes, not on string
    /// length and not on character count. A CJK-heavy delta is three times its
    /// character count here.
    ///
    /// Counted by hand rather than through `Encoding.UTF8`: Fable does not implement
    /// `GetByteCount`, and `GetBytes(...).Length` would allocate the whole buffer on
    /// every step of the truncation search. The arithmetic is UTF-8's definition —
    /// 1 byte below U+0080, 2 below U+0800, 4 for a surrogate pair, 3 otherwise.
    ///
    /// An unpaired surrogate counts as 3, matching what both runtimes emit for the
    /// U+FFFD replacement they substitute.
    let byteCount (text: string) : int =
        if isNull text then
            0
        else
            let mutable total = 0
            let mutable index = 0

            while index < text.Length do
                let code = int text.[index]

                if code < 0x80 then
                    total <- total + 1
                elif code < 0x800 then
                    total <- total + 2
                elif code >= 0xD800 && code <= 0xDBFF && index + 1 < text.Length then
                    let low = int text.[index + 1]

                    if low >= 0xDC00 && low <= 0xDFFF then
                        total <- total + 4
                        index <- index + 1
                    else
                        total <- total + 3
                else
                    total <- total + 3

                index <- index + 1

            total
