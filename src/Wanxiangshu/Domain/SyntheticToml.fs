namespace Wanxiangshu.Domain

open System
open System.Text

/// ARCH-010: the one canonical writer for runtime synthetic TOML.
///
/// The clause says 「字符串写法只有一个 owner。各业务模块不得分别决定引号、转义、换行处理、缩进或
/// closing delimiter 位置」. Until N3 that was true only incidentally — `BloggerToml` was the sole
/// producer, so it could hold the rules privately and still satisfy the clause. The moment a second
/// surface needs to render a value, "only one owner" has to become structural or the second surface
/// copies the logic and the dialect the clause forbids exists.
///
/// So this module owns the string rules and the document layout, and every synthetic surface renders
/// through it. It knows nothing about Blogger, forks, or any local schema: those live with their
/// producers, which is what 「不引入统一 envelope」 means.
///
/// There is deliberately no parser. ARCH-010 forbids business logic that reads this text back.
[<RequireQualifiedAccess>]
module SyntheticToml =

    /// ARCH-010: CRLF and lone CR normalise to LF before anything else looks at the text. Without
    /// this the same logical content renders as different bytes depending on which platform produced
    /// it, and 「同一 semantic input 必须产生相同 bytes」 fails for a reason nobody can see.
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
    /// A literal multi-line string processes NO escapes, which is the only way to carry code, JSON
    /// and logs verbatim. Two things it cannot hold: `'''`, which would close it early and let the
    /// remainder escape into the document structure, and raw control characters other than tab and
    /// newline, which TOML forbids in any string.
    ///
    /// It used to also reject a trailing `'`, because a closing delimiter written immediately after
    /// the last content character formed `''''`. ARCH-010 puts the delimiter on its own line, so
    /// that case cannot arise and the check is gone rather than kept "for safety" — a predicate
    /// nothing can fail is indistinguishable from one that is wrong.
    let private literalSafeRange (text: string) (origin: int) (stop: int) =
        // DSL-MUTABLE: algorithm-scratch — literal-safe index cursor
        let mutable index = origin
        // DSL-MUTABLE: algorithm-scratch — consecutive single-quote run
        let mutable quoteRun = 0
        // DSL-MUTABLE: algorithm-scratch — still-safe flag
        let mutable safe = true

        while safe && index < stop do
            let c = text.[index]

            if c = '\'' then
                quoteRun <- quoteRun + 1

                if quoteRun >= 3 then
                    safe <- false
            else
                quoteRun <- 0

                if c <> '\n' && c <> '\t' && Char.IsControl c then
                    safe <- false

            index <- index + 1

        safe

    let private literalSafe (text: string) =
        literalSafeRange text 0 text.Length

    /// ARCH-010 string selection. Deterministic, and genuinely parseable:
    ///
    ///   no newline                  → basic `"…"` with canonical escapes
    ///   newline and literal-safe    → `'''` + verbatim body + closing delimiter on its own line
    ///   anything else               → basic `"…"` with everything escaped
    ///
    /// The first test is only "does it contain a newline". Every other character a body can hold —
    /// tab, NUL, DEL — has a basic-string escape, so a newline is the one thing that forces the
    /// multi-line form.
    ///
    /// ── why `'''` and not `"""` ─────────────────────────────────────────────
    ///
    /// A multi-line BASIC string still processes escape sequences, and this notation has to carry
    /// tool output, file contents, diffs and compiler logs into the value unchanged. A body holding
    /// a backslash — every regex, every Windows path, every non-trivial tool-call argument — then
    /// has only two outcomes, and both break something the clause requires:
    ///
    ///   backslash left alone   `\d` is not a valid TOML escape, so the document does not parse
    ///   backslash escaped      the model reads `\\d+` where the tool emitted `\d+`, which is a
    ///                          distortion of the very data the payload exists to report
    ///
    /// A literal multi-line string processes nothing, so the dilemma disappears. Which is also why
    /// no format indentation is injected: TOML does not de-indent a literal string, so indenting the
    /// body would put those spaces IN the value — the renderer corrupting data it promised to pass
    /// through.
    ///
    /// ── the fallback is not a delimiter choice ──────────────────────────────
    ///
    /// ARCH-010 forbids picking between multi-line delimiters by content. The `else` branch is not
    /// that: a body containing `'''` or a raw control character has NO legal multi-line
    /// representation, so it goes to the single-line form. The rule is decidable and the same input
    /// always lands the same way.
    ///
    /// The single-line form still escapes everything, and that asymmetry is required rather than
    /// accidental: a single-line basic string has no raw variant, since TOML would read `"a\b"` as a
    /// backspace. Only the multi-line form can be verbatim, which is what it is for.
    ///
    /// Being genuinely parseable is a hard requirement, not decoration. One-way means no business
    /// logic may parse this back; it does not license emitting invalid TOML. Parseability is the only
    /// mechanically checkable property this notation has, and every gate, golden test and
    /// containment assertion rests on it.
    let renderString (raw: string) : string =
        let text = normalizeNewlines raw

        if not (text.Contains "\n") then
            "\"" + escapeBasic text + "\""
        elif literalSafe text then
            "'''\n" + text + "\n'''"
        else
            "\"" + escapeBasic text + "\""

    /// One instruction line, or several if the text itself spans lines.
    ///
    /// Splitting is not a convenience: a `\n` inside a comment would end the comment and put the
    /// remainder at top level as syntax, which is the containment failure ARCH-010 names. Blank
    /// lines render as a bare `#` so the header stays one contiguous comment block — a truly empty
    /// line would end the header and make everything after it a second, illegal one.
    let comment (text: string) : string =
        normalizeNewlines text
        |> fun normalized -> normalized.Split '\n'
        |> Array.map (fun line -> if line = "" then "#" else "# " + line)
        |> String.concat "\n"

    /// `name = <rendered value>`. The value must already be rendered by `renderString`.
    let field (name: string) (renderedValue: string) : string = name + " = " + renderedValue


    /// A `[name]` table: header plus its fields, as one block.
    let tableEntry (name: string) (fields: string list) : string =
        String.concat "\n" (("[" + name + "]") :: fields)

    /// A `[[name]]` entry: the header plus its own fields, as one block.
    ///
    /// Takes the fields rather than returning a bare header so an entry cannot be assembled with
    /// something else accidentally between the header and its body — which in TOML would silently
    /// reassign those fields to a different table.
    let tableArrayEntry (name: string) (fields: string list) : string =
        String.concat "\n" (("[[" + name + "]]") :: fields)


    let renderBool (value: bool) = if value then "true" else "false"

    let renderInt (value: int64) = string value

    /// Non-integer finite floats only. If the runtime string has no decimal and
    /// no exponent, append `.0` so the token cannot be read as an integer.
    let renderFloat (value: float) =
        let text = string value

        if text.IndexOf '.' >= 0 || text.IndexOf 'e' >= 0 || text.IndexOf 'E' >= 0 then
            text
        else
            text + ".0"

    let private isBareKey (name: string) =
        name.Length > 0
        && name
           |> Seq.forall (fun c ->
               (c >= 'A' && c <= 'Z')
               || (c >= 'a' && c <= 'z')
               || (c >= '0' && c <= '9')
               || c = '_'
               || c = '-')

    let renderKey (name: string) =
        if isBareKey name then
            name
        else
            "\"" + escapeBasic (normalizeNewlines name) + "\""

    let private formatPath (segments: string list) =
        segments |> List.map renderKey |> String.concat "."

    /// JSON-compatible value tree for structured TOML data (js-* results).
    type DataValue =
        | Null
        | Bool of bool
        | Integer of int64
        | Float of float
        | String of string
        | Array of DataValue list
        | Object of (string * DataValue) list

    let rec private isPrimitiveTree (value: DataValue) =
        match value with
        | Bool _
        | Integer _
        | Float _
        | String _ -> true
        | Array items -> List.forall isPrimitiveTree items
        | Null
        | Object _ -> false

    let rec private renderInline (value: DataValue) : string =
        match value with
        | Bool b -> renderBool b
        | Integer n -> renderInt n
        | Float n -> renderFloat n
        | String s -> renderString s
        | Array items -> "[" + String.concat ", " (List.map renderInline items) + "]"
        | Null
        | Object _ -> "false"

    let rec encodeData (value: DataValue) : string list =
        let rec encodeObject (path: string list) (fields: (string * DataValue) list) : string list =
            let present =
                fields
                |> List.choose (fun (key, item) ->
                    match item with
                    | Null -> None
                    | _ -> Some(key, item))

            let localFields, nested =
                present
                |> List.fold
                    (fun (local, nested) (key, item) ->
                        match item with
                        | Object row -> local, nested @ encodeObject (path @ [ key ]) row
                        | Array items when
                            not (List.isEmpty items)
                            && List.forall
                                (function
                                | Object _ -> true
                                | _ -> false)
                                items
                            ->
                            let rows =
                                items
                                |> List.collect (function
                                    | Object row -> encodeObjectRow (path @ [ key ]) row
                                    | _ -> [])

                            local, nested @ rows
                        | Null -> local, nested
                        | _ -> local @ [ field (renderKey key) (renderInline item) ], nested)
                    ([], [])

            let self =
                match localFields, nested with
                | [], [] -> [ tableEntry (formatPath path) [] ]
                | [], _ -> []
                | _, _ -> [ tableEntry (formatPath path) localFields ]

            self @ nested

        and encodeObjectRow (path: string list) (fields: (string * DataValue) list) : string list =
            let present =
                fields
                |> List.choose (fun (key, item) ->
                    match item with
                    | Null -> None
                    | _ -> Some(key, item))

            let localFields, nested =
                present
                |> List.fold
                    (fun (local, nested) (key, item) ->
                        match item with
                        | Object row -> local, nested @ encodeObject (path @ [ key ]) row
                        | Array items when
                            not (List.isEmpty items)
                            && List.forall
                                (function
                                | Object _ -> true
                                | _ -> false)
                                items
                            ->
                            let rows =
                                items
                                |> List.collect (function
                                    | Object row -> encodeObjectRow (path @ [ key ]) row
                                    | _ -> [])

                            local, nested @ rows
                        | Null -> local, nested
                        | _ -> local @ [ field (renderKey key) (renderInline item) ], nested)
                    ([], [])

            tableArrayEntry (formatPath path) localFields :: nested

        match value with
        | Null -> []
        | Bool _
        | Integer _
        | Float _
        | String _ -> [ field "data" (renderInline value) ]
        | Array [] -> [ field "data" "[]" ]
        | Array items when
            List.forall
                (function
                | Object _ -> true
                | _ -> false)
                items
            ->
            items
            |> List.collect (function
                | Object row -> encodeObjectRow [ "data" ] row
                | _ -> [])
        | Array items -> [ field "data" (renderInline (DataValue.Array items)) ]
        | Object fields -> encodeObject [ "data" ] fields

    let encodeFs (rewritten: string list) (created: string list) : string list =
        let fields =
            [ if rewritten <> [] then
                  field "rewritten" (renderInline (DataValue.Array(List.map DataValue.String rewritten)))
              if created <> [] then
                  field "created" (renderInline (DataValue.Array(List.map DataValue.String created))) ]

        if fields = [] then [] else [ tableEntry "fs" fields ]

    /// Is this block a table header rather than a bare field?
    ///
    /// Reads the block's FIRST LINE, not the block. A multi-line value whose content begins with `[`
    /// — a log line, a JSON array, a rendered table — starts with `key = '''`, so it is correctly
    /// read as a field. Testing the whole block would misclassify exactly the payloads ARCH-010's
    /// containment rule exists to protect.
    let private isTableBlock (block: string) =
        let firstLine = (block.Split '\n').[0]
        firstLine.StartsWith "[" && firstLine.EndsWith "]"

    /// Assemble a payload: instruction comment header, one blank line, data body.
    ///
    /// This is where ARCH-010's layout rules stop being something a producer has to remember:
    ///
    ///   instruction-first        the header is a separate argument and always emitted first
    ///   exactly one blank line   inserted here, so no producer can pick a different spacing
    ///   no comment in the body   unexpressible: body blocks come from `field` and `tableArrayEntry`
    ///   three legal shapes       an empty header gives data-only, an empty body instruction-only,
    ///                            and neither adds the separator it would otherwise need
    ///   no body blank lines      the data body renders with single LF between blocks; only the
    ///                            header/body boundary carries the one blank line (ARCH-010).
    ///                            Multi-line TOML string values are content and keep their own
    ///                            newlines untouched.
    ///
    /// ── bare fields are emitted before tables, and that is load-bearing ─────
    ///
    /// In TOML a bare `key = value` after a `[[table]]` header belongs to THAT TABLE, not to the
    /// document. Measured: `[[t]]\nx = 2\n\na = 1` parses as `t = [{ x = 2, a = 1 }]` — the field is
    /// silently absorbed, with no error and no visible difference in the text.
    ///
    /// A composer that appends a top-level field after a table array therefore produces a document
    /// whose meaning is not what it reads like, and the failure is invisible in exactly the direction
    /// that matters: the payload still renders, still parses, and the model still sees the words.
    /// Sorting here makes the mistake unexpressible instead of asking every producer to remember it.
    ///
    /// The sort is stable, so a producer's own field order and table order both survive.
    let document (instructions: string list) (body: string list) : string =
        let header = instructions |> List.map comment
        let blocks = body |> List.filter (fun block -> block <> "")
        let bare, tables = blocks |> List.partition (isTableBlock >> not)
        let ordered = bare @ tables

        match header, ordered with
        | [], [] -> ""
        | _, [] -> String.concat "\n" header + "\n"
        | [], _ -> String.concat "\n" ordered + "\n"
        | _, _ -> String.concat "\n" header + "\n\n" + String.concat "\n" ordered + "\n"

    let private byteCountRange (text: string) (origin: int) (stop: int) : int =
        // DSL-MUTABLE: algorithm-scratch — span byte-count total accumulator
        let mutable total = 0
        // DSL-MUTABLE: algorithm-scratch — span byte-count index cursor
        let mutable index = origin

        while index < stop do
            let code = int text.[index]

            if code < 0x80 then
                total <- total + 1
            elif code < 0x800 then
                total <- total + 2
            elif code >= 0xD800 && code <= 0xDBFF && index + 1 < stop then
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

    /// Byte length of `escapeBasic` over `[origin, stop)` without allocating the
    /// escaped string. Arms and UTF-8 pairing must stay in lockstep with `escapeBasic`.
    let private escapeBasicByteCountRange (text: string) (origin: int) (stop: int) : int =
        // DSL-MUTABLE: algorithm-scratch — escaped-byte total accumulator
        let mutable total = 0
        // DSL-MUTABLE: algorithm-scratch — escaped-byte index cursor
        let mutable index = origin

        while index < stop do
            let code = int text.[index]

            if
                code = 0x22
                || code = 0x5C
                || code = 0x08
                || code = 0x09
                || code = 0x0A
                || code = 0x0C
                || code = 0x0D
            then
                total <- total + 2
                index <- index + 1
            elif code < 0x20 || code = 0x7F then
                total <- total + 6
                index <- index + 1
            elif code < 0x80 then
                total <- total + 1
                index <- index + 1
            elif code < 0x800 then
                total <- total + 2
                index <- index + 1
            elif code >= 0xD800 && code <= 0xDBFF && index + 1 < stop then
                let low = int text.[index + 1]

                if low >= 0xDC00 && low <= 0xDFFF then
                    total <- total + 4
                    index <- index + 2
                else
                    total <- total + 3
                    index <- index + 1
            else
                total <- total + 3
                index <- index + 1

        total

    let private rangeContainsNewline (text: string) (origin: int) (stop: int) =
        // DSL-MUTABLE: algorithm-scratch — newline-scan index
        let mutable index = origin
        // DSL-MUTABLE: algorithm-scratch — newline-scan found flag
        let mutable found = false

        while (not found) && index < stop do
            if text.[index] = '\n' then
                found <- true

            index <- index + 1

        found

    let private tripleQuoteJoins (head: string) (headLen: int) (tail: string) =
        let total = headLen + tail.Length

        let charAt i =
            if i < 0 || i >= total then
                Char.MinValue
            elif i < headLen then
                head.[i]
            else
                tail.[i - headLen]

        let startsAt i =
            charAt i = '\'' && charAt (i + 1) = '\'' && charAt (i + 2) = '\''

        (headLen >= 2 && startsAt (headLen - 2))
        || (headLen >= 1 && startsAt (headLen - 1))

    /// UTF-8 byte length of `renderString (text.Substring(0, length) + suffix)`
    /// without allocating the concatenation or the rendered form.
    ///
    /// `text` and `suffix` must already be newline-normalised. `renderString` would
    /// normalise first; doing it here would desynchronise `length` from the source
    /// the caller is searching.
    let renderStringByteCountPrefix (text: string) (length: int) (suffix: string) : int =
        let text = if isNull text then "" else text
        let suffix = if isNull suffix then "" else suffix

        let headLen =
            if length < 0 then 0
            elif length > text.Length then text.Length
            else length

        let hasNewline =
            suffix.Contains "\n" || rangeContainsNewline text 0 headLen

        let safe =
            literalSafeRange text 0 headLen
            && literalSafeRange suffix 0 suffix.Length
            && not (tripleQuoteJoins text headLen suffix)

        if not hasNewline then
            2 + escapeBasicByteCountRange text 0 headLen + escapeBasicByteCountRange suffix 0 suffix.Length
        elif safe then
            8 + byteCountRange text 0 headLen + byteCountRange suffix 0 suffix.Length
        else
            2 + escapeBasicByteCountRange text 0 headLen + escapeBasicByteCountRange suffix 0 suffix.Length

    /// UTF-8 byte count of rendered text.
    ///
    /// The limits every synthetic surface is measured against are byte limits, not character counts:
    /// a CJK-heavy payload is three times its character count here.
    ///
    /// Counted by hand rather than through `Encoding.UTF8`: Fable does not implement `GetByteCount`,
    /// and `GetBytes(...).Length` would allocate the whole buffer on every step of a truncation
    /// search. The arithmetic is UTF-8's definition — 1 byte below U+0080, 2 below U+0800, 4 for a
    /// surrogate pair, 3 otherwise.
    ///
    /// An unpaired surrogate counts as 3, matching what both runtimes emit for the U+FFFD
    /// replacement they substitute.
    let byteCount (text: string) : int =
        if isNull text then
            0
        else
            byteCountRange text 0 text.Length
