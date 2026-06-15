module VibeFs.Kernel.FuzzyGrepDetect

open System.Text.RegularExpressions

/// Escape every regex metacharacter — used to tell plain from regex patterns.
let private escapeRegex (pattern: string) : string =
    let escapeChar (m: Match) = "\\" + m.Value
    Regex.Replace(pattern, @"[.*+?^${}()|[\]\\]", escapeChar)

/// Detect whether a pattern is plain text, a valid regex, or a broken regex
/// (treated as plain).  Mirrors the host's three-mode search selection.
let private isValidRegex (pattern: string) : bool =
    try Regex(pattern) |> ignore; true
    with _ -> false

let detectGrepMode (pattern: string) : string =
    let escaped = escapeRegex pattern
    if pattern = escaped then "plain"
    elif isValidRegex pattern then "regex"
    else "plain"

/// Wildcard-only patterns (`.*`, `\s`, `.` alone) match everything and are
/// rejected as useless.  Only meaningful for regex/fuzzy modes.
let checkWildcardOnly (pattern: string) (mode: string) : bool =
    if mode = "plain" then false
    else Regex.IsMatch(pattern.Trim(), @"^(?:[.^$]*(?:[.][*+?]|\*|\+)[.^$]*|[.^$\s]*|\.\*\??|\.*[+?]?|\.+\??|\.|\*|\?)$")
