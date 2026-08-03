namespace Wanxiangshu.Domain

/// Preemptive bound for custom tool results returned to OpenCode Host.
///
/// Host (`packages/opencode/src/tool/truncate.ts`):
///
/// ```text
/// MAX_LINES = 2000
/// MAX_BYTES = 50 * 1024 = 51200
/// direction default = "head"   // keep start, drop end
/// pass-through iff lines ≤ 2000 AND utf8Bytes ≤ 51200
/// lines = text.split("\n").length   // trailing empty counts
/// ```
///
/// We bound before Host sees the result and keep the **tail**. Marker is a
/// fixed ASCII constant; content budgets are static literals — no iterative
/// search, no safety fudge factor:
///
/// ```text
/// Marker        = "...head truncated (tail kept)...\n\n"   // 34 bytes, ASCII
///
/// When content is non-empty:
///   result = Marker + content
///   result.lines = 2 + content.lines     // "m\n\nx".split → 3 = 2+1
///   result.bytes = 34 + content.bytes
///
/// ContentMaxLines = 2000 − 2  = 1998
/// ContentMaxBytes = 51200 − 34 = 51166
///
/// ⇒ result.lines ≤ 2000 ∧ result.bytes ≤ 51200  (Host no-ops)
/// ```
module ToolResultBound =

    // Not [<Literal>]: Fable drops unused compile-time literals from JS exports.
    // These are contract values tests and callers must observe.
    let HostMaxLines = 2000
    let HostMaxBytes = 51200

    /// Fixed ASCII prefix. UTF-8 byte length = char length = 34.
    /// Host line cost when concatenated with non-empty content: +2
    /// (`split("\n")` of `"…\n\n" + content` keeps the marker line + one empty).
    let Marker = "...head truncated (tail kept)...\n\n"
    let MarkerBytes = 34
    let ContentMaxLines = 1998
    let ContentMaxBytes = 51166

    let private isHighSurrogate (c: char) =
        let code = int c
        code >= 0xD800 && code <= 0xDBFF

    let private isLowSurrogate (c: char) =
        let code = int c
        code >= 0xDC00 && code <= 0xDFFF

    /// UTF-8 bytes of the scalar at `index` (handles surrogate pairs).
    /// Returns `(codeUnits, bytes)`.
    let private scalarAt (text: string) (index: int) : int * int =
        let c = text.[index]
        let code = int c

        if code < 0x80 then
            1, 1
        elif code < 0x800 then
            1, 2
        elif isHighSurrogate c && index + 1 < text.Length && isLowSurrogate text.[index + 1] then
            2, 4
        else
            // BMP non-ASCII, unpaired surrogate → 3 (matches SyntheticToml.byteCount)
            1, 3

    /// UTF-8-safe suffix with byte length ≤ `maxBytes`. Single reverse walk.
    let private utf8Tail (text: string) (maxBytes: int) : string =
        if maxBytes <= 0 then
            ""
        elif SyntheticToml.byteCount text <= maxBytes then
            text
        else
            let mutable endExclusive = text.Length
            let mutable start = text.Length
            let mutable acc = 0
            let mutable stop = false

            while start > 0 && not stop do
                // Step one scalar backward.
                let prev =
                    if
                        start >= 2
                        && isLowSurrogate text.[start - 1]
                        && isHighSurrogate text.[start - 2]
                    then
                        start - 2
                    else
                        start - 1

                let _, bytes = scalarAt text prev

                if acc + bytes > maxBytes then
                    stop <- true
                else
                    acc <- acc + bytes
                    start <- prev

            if start >= endExclusive then
                ""
            else
                text.Substring(start, endExclusive - start)

    /// Tail under ContentMaxLines / ContentMaxBytes. Host-compatible accounting:
    /// `split('\n')` keeps empties; inter-line newline costs 1 byte.
    let private takeTail (text: string) : string =
        let lines = text.Split('\n')

        let rec collect index acc bytes =
            if index < 0 || List.length acc >= ContentMaxLines then
                acc
            else
                let line = lines.[index]
                let lineBytes = SyntheticToml.byteCount line
                let size = lineBytes + (if List.isEmpty acc then 0 else 1)

                if bytes + size > ContentMaxBytes then
                    if List.isEmpty acc then
                        let tail = utf8Tail line ContentMaxBytes
                        if tail = "" then [] else [ tail ]
                    else
                        acc
                else
                    collect (index - 1) (line :: acc) (bytes + size)

        collect (lines.Length - 1) [] 0 |> String.concat "\n"

    /// Bound a custom tool result. Under Host limits → identity.
    /// Over → `Marker + tail`, sized so Host does not re-truncate.
    let bound (text: string) : string =
        if isNull text || text = "" then
            text
        else
            let totalBytes = SyntheticToml.byteCount text
            let totalLines = text.Split('\n').Length

            if totalLines <= HostMaxLines && totalBytes <= HostMaxBytes then
                text
            else
                Marker + takeTail text
