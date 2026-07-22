module Wanxiangshu.Runtime.CapsFormat

open Wanxiangshu.Runtime.Tooling.ToolOutputToml

/// A discovered capability file: its absolute path, display label, and content.
type CapsFile =
    { filePath: string
      label: string
      content: string }

/// Stable fingerprint over caps files — kernel decides WHAT to hash, the
/// injected `hashFn` decides HOW (e.g. Shell.Crypto.sha256HexTruncated).
let stableFingerprint (hashFn: string -> string) (capsFiles: CapsFile list) : string =
    capsFiles
    |> List.sortBy (fun cap -> cap.label, cap.filePath)
    |> List.collect (fun cap -> [ cap.filePath; "\u0000"; cap.content; "\u0000" ])
    |> String.concat ""
    |> hashFn

/// Wrap already-discovered capability files as a TOML block. Pure:
/// file discovery lives in the shell; this only formats.
let buildCapitalsContext (files: CapsFile list) : string =
    let items =
        files
        |> List.map (fun f ->
            { CapsItem.label = f.label
              content = f.content })

    renderCaps items

/// OpenCode-native read tool output shape (XML tags). Host mimicry — not TOML.
let formatReadOutput (filePath: string) (content: string) (startLine: int) (totalLines: int option) : string =
    let lines = content.Split('\n')
    let reported = defaultArg totalLines lines.Length

    let numbered =
        lines
        |> Array.mapi (fun i line -> $"{startLine + i}: {line}")
        |> String.concat "\n"

    String.concat
        "\n"
        [ $"<path>{filePath}</path>"
          "<type>file</type>"
          "<content>"
          numbered
          ""
          $"(End of file - total {reported} lines)"
          "</content>" ]
