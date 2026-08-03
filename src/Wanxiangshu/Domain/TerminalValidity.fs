namespace Wanxiangshu.Domain

open System
open System.Text.RegularExpressions

/// CTX-004: the one content-level validity check in the system.
///
/// The clause is deliberately narrow. It answers "is this produced text usable
/// as a result", not "why did the request fail" — CTX-005 forbids the second
/// question entirely. So this module reads only the produced text and knows
/// nothing about providers, error names or context limits.
///
/// Single owner. Every consumer that needs "was this terminal usable" calls
/// here: the turn classifier (FALLBACK-008 repair), the Companion entry writer
/// and the squash writer (CTX-007), and the probe promote gate (CTX-012). A
/// second copy could answer differently for the same text, which would let one
/// caller commit a fact another caller would have refused.
[<RequireQualifiedAccess>]
module TerminalValidity =

    /// Containment, not well-formedness: a broken or partial tag still means the
    /// model was trying to call a tool rather than answer.
    let private xmlMarkup =
        Regex(
            "<(?:/?\\s*(?:tool_call|use_tool|call|function_call|invoke)\\b[^>]*>?)|</(?:tool_call|use_tool|call|function_call|invoke)\\s*>",
            RegexOptions.Compiled ||| RegexOptions.IgnoreCase
        )

    /// Why a terminal was refused. A DU rather than a bool so a caller that must
    /// report the reason to a human (HOST-007 diagnostics) does not re-test the
    /// text to find out which case it hit.
    ///
    /// These are not failure classifications: they describe the SHAPE of a
    /// successful provider response, and both are observable from the text alone.
    /// CTX-005's prohibition is on inferring the CAUSE of a `Failed` attempt.
    [<RequireQualifiedAccess>]
    type Rejection =
        /// Nothing the model meant as an answer.
        | Empty
        /// Tool-call markup where prose was required.
        | XmlOnly

    let describe (rejection: Rejection) =
        match rejection with
        | Rejection.Empty -> "empty terminal"
        | Rejection.XmlOnly -> "XML-only terminal"

    /// CTX-004 verbatim: non-empty and not an XML-only terminal.
    let check (text: string) : Result<unit, Rejection> =
        if String.IsNullOrWhiteSpace text then Error Rejection.Empty
        elif xmlMarkup.IsMatch text then Error Rejection.XmlOnly
        else Ok()

    let isValid (text: string) : bool =
        match check text with
        | Ok() -> true
        | Error _ -> false
