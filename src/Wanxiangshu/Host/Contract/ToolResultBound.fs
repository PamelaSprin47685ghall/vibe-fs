namespace Wanxiangshu.Host.Contract

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction

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
            // BMP non-ASCII, unpaired surrogate → 3 (matches LlmFacing.byteCount)
            1, 3

    let private scalarStepBack (text: string) (start: int) : int =
        if
            start >= 2
            && isLowSurrogate text.[start - 1]
            && isHighSurrogate text.[start - 2]
        then
            start - 2
        else
            start - 1

    let rec private walkUtf8Tail (text: string) (maxBytes: int) (acc: int) (start: int) : int =
        let atEnd = start <= 0
        let prev = if atEnd then 0 else scalarStepBack text start
        let bytes = if atEnd then 0 else snd (scalarAt text prev)

        if atEnd then start
        elif acc + bytes > maxBytes then start
        else walkUtf8Tail text maxBytes (acc + bytes) prev

    let private sliceFrom (text: string) (start: int) : string =
        if start >= text.Length then "" else text.Substring(start)

    /// UTF-8-safe suffix with byte length ≤ `maxBytes`. Single reverse walk.
    let private utf8Tail (text: string) (maxBytes: int) : string =
        if maxBytes <= 0 then
            ""
        elif LlmFacing.byteCount text <= maxBytes then
            text
        else
            sliceFrom text (walkUtf8Tail text maxBytes 0 text.Length)

    /// Locate segment start (one past previous `\n`) and the newline index (`-1` if none).
    let private findLineBounds (text: string) (segEnd: int) : int * int =
        // DSL-MUTABLE: algorithm-scratch — backward newline-scan cursor
        let mutable p = segEnd - 1

        while p >= 0 && text.[p] <> '\n' do
            p <- p - 1

        p + 1, p

    let private overflowTail (text: string) (segStart: int) (segEnd: int) : string list =
        let tail = utf8Tail (text.Substring(segStart, segEnd - segStart)) ContentMaxBytes
        if tail = "" then [] else [ tail ]

    let rec private collectTail
        (text: string)
        (segments: string list)
        (accBytes: int)
        (count: int)
        (segEnd: int)
        : string list =
        let segStart, newlineAt = findLineBounds text segEnd
        let line = text.Substring(segStart, segEnd - segStart)
        let lineBytes = LlmFacing.byteCount line
        let size = lineBytes + (if count = 0 then 0 else 1)
        let overBudget = count >= ContentMaxLines || accBytes + size > ContentMaxBytes

        if overBudget && count = 0 then
            overflowTail text segStart segEnd
        elif overBudget then
            segments
        elif newlineAt < 0 then
            line :: segments
        else
            collectTail text (line :: segments) (accBytes + size) (count + 1) newlineAt

    /// Tail under ContentMaxLines / ContentMaxBytes. Host-compatible accounting:
    /// `split('\n')` keeps empties; inter-line newline costs 1 byte.
    /// Single backward walk from the end — never allocates a full line array.
    let private takeTail (text: string) : string =
        String.concat "\n" (collectTail text [] 0 0 text.Length)

    let private newlineBump (n: int) (c: char) = if c = '\n' then n + 1 else n

    let private countLines (text: string) : int =
        // DSL-MUTABLE: algorithm-scratch — newline-derived line counter
        let mutable n = 1

        for c in text do
            n <- newlineBump n c

        n

    let private boundNonEmpty (text: string) : string =
        let totalBytes = LlmFacing.byteCount text
        // split('\n').length == newline count + 1 (trailing empty counts).
        let totalLines = countLines text

        if totalLines <= HostMaxLines && totalBytes <= HostMaxBytes then
            text
        else
            Marker + takeTail text

    /// Bound a custom tool result. Under Host limits → identity.
    /// Over → `Marker + tail`, sized so Host does not re-truncate.
    let bound (text: string) : string =
        if isNull text || text = "" then
            text
        else
            boundNonEmpty text
