module Wanxiangshu.Runtime.OpencodeReadSynth

open Fable.Core.JsInterop
open Wanxiangshu.Runtime.CapsFormat

/// Shared OpenCode-native `read` tool synthesis. Caps inject and Semble inject
/// both emit this shape so toModelMessages sees the same bytes as a real read.

let private previewLines (content: string) : string =
    content.Split('\n')
    |> Array.truncate 20
    |> String.concat "\n"

let buildReadMetadata
    (filePath: string)
    (content: string)
    (lineStart: int)
    (totalLines: int)
    (truncated: bool)
    : obj =
    let lineCount =
        if content = "" then
            0
        else
            content.Split('\n').Length

    let lineEnd =
        if lineCount = 0 then
            max 0 (lineStart - 1)
        else
            lineStart + lineCount - 1

    createObj
        [ "preview", box (previewLines content)
          "truncated", box truncated
          "loaded", box [||]
          "display",
          box (
              createObj
                  [ "type", box "file"
                    "path", box filePath
                    "text", box content
                    "lineStart", box lineStart
                    "lineEnd", box lineEnd
                    "totalLines", box totalLines
                    "truncated", box truncated ]
          ) ]

let buildCompletedReadState
    (filePath: string)
    (content: string)
    (lineStart: int)
    (totalLines: int)
    (input: obj)
    : obj =
    let lineCount =
        if content = "" then
            0
        else
            content.Split('\n').Length

    let lineEnd =
        if lineCount = 0 then
            max 0 (lineStart - 1)
        else
            lineStart + lineCount - 1

    let truncated = lineEnd < totalLines
    let output = formatReadOutput filePath content lineStart (Some totalLines)
    let t = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

    createObj
        [ "status", box "completed"
          "input", input
          "output", box output
          "title", box filePath
          "metadata", box (buildReadMetadata filePath content lineStart totalLines truncated)
          "time", box (createObj [ "start", box t; "end", box (t + 1L) ]) ]
