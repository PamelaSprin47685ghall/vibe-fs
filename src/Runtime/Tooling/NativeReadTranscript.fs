module Wanxiangshu.Runtime.NativeReadTranscript

/// Native read transcript — a host-independent representation of a file read
/// operation. Caps and Semble produce this and then each host's encoder
/// converts it to the native message shape.
type NativeReadTranscript =
    { Path: string
      Offset: int option
      Limit: int option
      StartLine: int
      TotalLines: int
      Lines: string array
      Truncated: bool }

/// Format numbered lines the same way the real FileSys.read does.
/// This is the single shared formatter — caps, semble, and real reads all
/// call it so the output is identical.
let formatNumberedLines (startLine: int) (lines: string array) : string =
    lines
    |> Array.mapi (fun i line -> sprintf "%6d|%s" (startLine + i) (line.TrimEnd('\r')))
    |> String.concat "\n"

/// Format a NativeReadTranscript as a string, matching the real FileSys.read output format.
let formatTranscript (transcript: NativeReadTranscript) : string =
    let body =
        if transcript.Lines.Length = 0 then
            "(empty file)"
        else
            formatNumberedLines transcript.StartLine transcript.Lines

    let header = $"{transcript.Path}"

    let footer =
        let total = transcript.TotalLines
        let shown = transcript.StartLine + transcript.Lines.Length - 1

        let truncated =
            if transcript.Truncated then
                $" (showing {shown - transcript.StartLine + 1} of {total} lines)"
            else
                ""

        if transcript.TotalLines > 0 then
            $"(End of file - total {total} lines{truncated})"
        else
            "(End of file)"

    $"{header}\n{body}\n{footer}"
