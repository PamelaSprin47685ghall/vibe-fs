namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System

/// JS-native terminal/process policy edge. Process handles remain opaque and
/// command validation/error meaning is represented as a plain result.
module PtySurface =
    let validateCommand (command: string) : obj =
        if String.IsNullOrWhiteSpace command then
            box {| ok = false; error = "PTY command is required" |}
        else
            box {| ok = true; command = command |}

    let writeText (text: string) : string =
        if String.IsNullOrEmpty text then text
        elif text.EndsWith("\n", StringComparison.Ordinal) || text.EndsWith("\r", StringComparison.Ordinal) then text
        else text + "\n"

    let unknown (id: string) : obj = box {| ok = false; error = sprintf "Unknown PTY id: %s" id |}
