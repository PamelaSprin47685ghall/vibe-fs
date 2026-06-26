module VibeFs.Kernel.CapsFormat

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.PromptFrontMatter

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

let formatReadOutput (filePath: string) (content: string) : string =
    let lines = content.Split('\n')
    let numbered = lines |> Array.mapi (fun i line -> $"{i + 1}: {line}") |> String.concat "\n"
    String.concat "\n" [
        $"<path>{filePath}</path>"
        "<type>file</type>"
        "<content>"
        numbered
        ""
        $"(End of file - total {lines.Length} lines)"
        "</content>"
    ]
