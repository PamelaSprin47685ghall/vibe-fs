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
