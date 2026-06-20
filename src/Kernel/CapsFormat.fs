module VibeFs.Kernel.CapsFormat

/// Escape a string for safe use inside an XML attribute value.
let escapeXmlAttr (value: string) : string =
    value.Replace("&", "&amp;").Replace("\"", "&quot;")
         .Replace("'", "&apos;").Replace("<", "&lt;").Replace(">", "&gt;")

/// A discovered capability file: its absolute path, display label, and content.
type CapsFile = { filePath: string; label: string; content: string }

/// Wrap already-discovered capability files in `<caps-context>` blocks.  Pure:
/// file discovery lives in the shell; this only formats.
let buildCapitalsContext (files: CapsFile list) : string =
    files
    |> List.map (fun f -> $"<caps-context file=\"{escapeXmlAttr f.label}\">\n{f.content}\n</caps-context>")
    |> String.concat "\n\n"

/// Stable fingerprint over caps files — kernel decides WHAT to hash, the
/// injected `hashFn` decides HOW (e.g. Shell.Crypto.sha256HexTruncated).
let stableFingerprint (hashFn: string -> string) (capsFiles: CapsFile list) : string =
    capsFiles
    |> List.collect (fun cap -> [ cap.filePath; "\u0000"; cap.content; "\u0000" ])
    |> String.concat ""
    |> hashFn

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
