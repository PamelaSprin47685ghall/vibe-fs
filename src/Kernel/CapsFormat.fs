module Wanxiangshu.Kernel.CapsFormat

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.PromptFrontMatter

/// A discovered capability file: its absolute path, display label, and content.
type CapsFile = { filePath: string; label: string; content: string }

/// Stable fingerprint over caps files — kernel decides WHAT to hash, the
/// injected `hashFn` decides HOW (e.g. Shell.Crypto.sha256HexTruncated).
let stableFingerprint (hashFn: string -> string) (capsFiles: CapsFile list) : string =
    capsFiles
    |> List.collect (fun cap -> [ cap.filePath; "\u0000"; cap.content; "\u0000" ])
    |> String.concat ""
    |> hashFn

/// Wrap already-discovered capability files as a YAML front-matter block. Pure:
/// file discovery lives in the shell; this only formats.
let buildCapitalsContext (files: CapsFile list) : string =
    let items =
        files |> List.map (fun f ->
            createObj [ "label", box f.label; "content", box f.content ])
    frontMatter [ yamlSeqField "caps" items ]

let formatReadOutput (filePath: string) (content: string) (startLine: int) : string =
    let lines = content.Split('\n')
    let numbered = lines |> Array.mapi (fun i line -> $"{startLine + i}: {line}") |> String.concat "\n"
    String.concat "\n" [
        $"<path>{filePath}</path>"
        "<type>file</type>"
        "<content>"
        numbered
        ""
        $"(End of file - total {lines.Length} lines)"
        "</content>"
    ]

/// Stable fingerprint over a read tool's output body. Recognizes both
///   `<line>: <content>` (CapsFormat/Semble injected) and
///   `     <line>|<content>` (Shell.FileSys native).
/// Returns `None` when fewer than two numbered lines are found (directory
/// listings, errors, plain prose) so the caller can fall back to substring
/// matching without losing existing semantics.
let private skipWs (s: string) (start: int) : int =
    let rec loop i =
        if i >= s.Length then i
        elif s.[i] = ' ' || s.[i] = '\t' then loop (i + 1)
        else i
    loop start

let private skipDigits (s: string) (start: int) : int =
    let rec loop i =
        if i >= s.Length then i
        elif s.[i] >= '0' && s.[i] <= '9' then loop (i + 1)
        else i
    loop start

let private skipOneSpace (s: string) (start: int) : int =
    if start < s.Length && s.[start] = ' ' then start + 1 else start

let private parseLine (raw: string) : (int * string) option =
    let s = raw.TrimEnd('\r')
    let lineStart = skipWs s 0
    let numEnd = skipDigits s lineStart
    if numEnd = lineStart || numEnd >= s.Length then None
    else
        let lineNo = int (s.Substring(lineStart, numEnd - lineStart))
        let sep = s.[numEnd]
        if sep <> ':' && sep <> '|' then None
        else
            let bodyStart = skipOneSpace s (numEnd + 1)
            Some (lineNo, s.Substring(bodyStart))

let readFingerprint (output: string) : string option =
    if String.length output = 0 then None
    else
        let pairs =
            output.Split('\n')
            |> Array.choose parseLine
        if pairs.Length < 2 then None
        else
            pairs
            |> Array.distinctBy fst
            |> Array.sortBy fst
            |> Array.map snd
            |> String.concat "\n"
            |> Some
