module Wanxiangshu.Kernel.FileSwap

/// A 1-based, begin-inclusive, end-exclusive line range.
type LineRange = { Begin: int; EndExclusive: int }

/// A swap request: the two (path, range) pairs.
type SwapRequest =
    { Path0: string
      Range0: LineRange
      Path1: string
      Range1: LineRange }

/// Validation errors for a swap request.
type SwapValidationError =
    | EmptyPath of field: string
    | InvalidRange of field: string
    | OutOfBounds of path: string * range: LineRange * lineCount: int
    | OverlappingRanges

/// Lossless text document representation that preserves newline style and
/// final-newline state. This is critical because Split/Join on '\n' destroys
/// CRLF and trailing-newline conventions.
type TextDocument =
    { Lines: string array
      Newline: string
      HasFinalNewline: bool }

/// Detect the dominant newline style in a text.
let private detectNewline (text: string) : string =
    let crlfCount =
        let mutable n = 0
        let mutable i = 0

        while i < text.Length - 1 do
            if text.[i] = '\r' && text.[i + 1] = '\n' then
                n <- n + 1
                i <- i + 2
            else
                i <- i + 1

        n

    if crlfCount > 0 then "\r\n" else "\n"

/// Parse text into a lossless document.
let parseTextDocument (text: string) : TextDocument =
    let newline = detectNewline text
    let hasFinalNewline = text.EndsWith(newline)

    let lines =
        if text = "" then
            [||]
        else
            let trimmed =
                if hasFinalNewline then
                    text.Substring(0, text.Length - newline.Length)
                else
                    text

            if trimmed = "" then
                [||]
            else
                trimmed.Split(newline)
                |> Array.map (fun line ->
                    if line.EndsWith("\r") then
                        line.Substring(0, line.Length - 1)
                    else
                        line)

    { Lines = lines
      Newline = newline
      HasFinalNewline = hasFinalNewline }

/// Reconstruct text from a document, preserving newline style and final newline.
let renderTextDocument (doc: TextDocument) : string =
    let body = String.concat doc.Newline doc.Lines

    if doc.HasFinalNewline && doc.Lines.Length > 0 then
        body + doc.Newline
    elif doc.HasFinalNewline then
        doc.Newline
    else
        body

/// Check if two ranges overlap (for same-file swaps).
let overlaps (a: LineRange) (b: LineRange) : bool =
    max a.Begin b.Begin < min a.EndExclusive b.EndExclusive

/// Validate a swap request.
let validate
    (path0: string)
    (range0: LineRange)
    (path1: string)
    (range1: LineRange)
    (lineCount0: int)
    (lineCount1: int)
    : Result<unit, SwapValidationError> =

    // Empty paths
    if System.String.IsNullOrWhiteSpace path0 then
        Error(EmptyPath "path0")
    elif System.String.IsNullOrWhiteSpace path1 then
        Error(EmptyPath "path1")
    // Invalid ranges
    elif range0.Begin < 1 then
        Error(InvalidRange "begin0")
    elif range1.Begin < 1 then
        Error(InvalidRange "begin1")
    elif range0.EndExclusive <= range0.Begin then
        Error(InvalidRange "endExclusive0")
    elif range1.EndExclusive <= range1.Begin then
        Error(InvalidRange "endExclusive1")
    // Out of bounds
    elif range0.EndExclusive > lineCount0 + 1 then
        Error(OutOfBounds(path0, range0, lineCount0))
    elif range1.EndExclusive > lineCount1 + 1 then
        Error(OutOfBounds(path1, range1, lineCount1))
    // Overlap check — only for same file
    elif path0 = path1 && overlaps range0 range1 then
        Error OverlappingRanges
    else
        Ok()

/// Canonicalize a path for comparison purposes. This handles simple cases;
/// the full FS-level canonicalization happens in the Runtime layer.
let canonicalizePath (path: string) : string =
    let normalized = path.Replace('\\', '/')

    // Resolve simple .. segments
    let parts = normalized.Split('/') |> Array.toList

    let resolved =
        parts
        |> List.fold
            (fun acc part ->
                match part with
                | "." -> acc
                | ".." ->
                    match acc with
                    | _ :: rest -> rest
                    | [] -> []
                | _ -> acc @ [ part ])
            []

    String.concat "/" resolved
