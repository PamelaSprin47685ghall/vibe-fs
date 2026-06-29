module Wanxiangshu.Kernel.CapsFormat

open Fable.Core
open Fable.Core.JsInterop

/// A discovered capability file: its absolute path, display label, and content.
type CapsFile = { filePath: string; label: string; content: string }

/// Stable fingerprint over caps files — kernel decides WHAT to hash, the
/// injected `hashFn` decides HOW (e.g. Shell.Crypto.sha256HexTruncated).
let stableFingerprint (hashFn: string -> string) (capsFiles: CapsFile list) : string =
    capsFiles
    |> List.collect (fun cap -> [ cap.filePath; "\u0000"; cap.content; "\u0000" ])
    |> String.concat ""
    |> hashFn

type ReadSlice = { raw: string[]; offset: int; totalLines: int; more: bool; cut: bool }

let sliceFromContent (content: string) : ReadSlice =
    let raw = if content = "" then [||] else content.Split('\n')
    { raw = raw; offset = 1; totalLines = raw.Length; more = false; cut = false }

let formatReadFooter (s: ReadSlice) : string =
    let last = s.offset + s.raw.Length - 1
    let next = last + 1
    if s.cut then $"(Output capped at 50 KB. Showing lines {s.offset}-{last}. Use offset={next} to continue.)"
    elif s.more then $"(Showing lines {s.offset}-{last} of {s.totalLines}. Use offset={next} to continue.)"
    else $"(End of file - total {s.totalLines} lines)"

let formatReadOutput (filePath: string) (s: ReadSlice) : string =
    let numbered = s.raw |> Array.mapi (fun i line -> $"{s.offset + i}: {line}") |> String.concat "\n"
    String.concat "\n" [
        $"<path>{filePath}</path>"
        "<type>file</type>"
        "<content>"
        numbered
        ""
        formatReadFooter s
        "</content>"
    ]
