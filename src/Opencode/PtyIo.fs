module Wanxiangshu.Opencode.PtyIo

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolPermission
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.PromptFrontMatter
open Wanxiangshu.Opencode.ToolSchema
open Wanxiangshu.Opencode.PtySpawn

module Dyn = Wanxiangshu.Shell.Dyn

let ptyWriteTool (host: Host) : obj =
    define "Send input to a PTY session. Supports escape sequences like \\x03 for Ctrl+C, \\n for newline, \\r for carriage return."
        (createObj [
            "id", box (strReq "The PTY session ID (e.g., pty_a1b2c3d4)")
            "data", box (strReq "The input data to send to the PTY")
        ])
        (fun args context ->
            checkExecPerm host context
            let id = string args?``id``
            let data = string args?data
            promise {
                let! mgr = getManager ()
                let success = unbox<bool> (mgr?write(id, data))
                if not success then failwithf "Failed to write to PTY '%s'" id
                let preview = if data.Length > 50 then data.Substring(0, 50) + "..." else data
                let display = preview.Replace("\x03", "^C").Replace("\x04", "^D").Replace("\n", "\\n").Replace("\r", "\\r")
                return frontMatterPrompt [ "id", box id; "bytes", box data.Length ] (sprintf "Sent: \"%s\"" display)
            })

let ptyReadTool (host: Host) : obj =
    define "Read output buffer from a PTY session with pagination (offset/limit) and optional regex pattern filtering."
        (createObj [
            "id", box (strReq "The PTY session ID (e.g., pty_a1b2c3d4)")
            "offset", box (numOpt "Line number to start reading from (0-based, defaults to 0). When using pattern, this applies to filtered matches.")
            "limit", box (numOpt "Number of lines to read (defaults to 500). When using pattern, this applies to filtered matches.")
            "pattern", box (strOpt "Regex pattern to filter lines. When set, only matching lines are returned, then offset/limit apply to the matches.")
            "ignoreCase", box (boolOpt "Case-insensitive pattern matching (default: false)")
        ])
        (fun args context ->
            checkExecPerm host context
            let id = string args?``id``
            let offset' = if Dyn.isNullish (Dyn.get args "offset") then 0 else unbox<int> args?offset
            let limit' = if Dyn.isNullish (Dyn.get args "limit") then 500 else unbox<int> args?limit
            let pattern = Dyn.str args "pattern"
            promise {
                let! mgr = getManager ()
                let session = mgr?``get``(id)
                if Dyn.isNullish session then failwithf "PTY session not found: %s" id
                if pattern = "" then
                    let result = mgr?read(id, offset', limit')
                    if Dyn.isNullish result then failwithf "PTY session not found: %s" id
                    let lines : string array = unbox (result?lines)
                    let totalLines = unbox<int> result?totalLines
                    let hasMore = unbox<bool> result?hasMore
                    let resultOffset = unbox<int> result?offset
                    let fields = [
                        "id", box id
                        "status", box (string session?status)
                        "offset", box resultOffset
                        "returned", box lines.Length
                        "total_lines", box totalLines
                        "has_more", box hasMore
                    ]
                    let sb = ResizeArray<string>()
                    for i in 0 .. lines.Length - 1 do
                        sb.Add(lines.[i])
                    sb.Add("")
                    if hasMore then
                        sb.Add(sprintf "(Buffer has more lines. Use offset=%d to read beyond line %d)" (resultOffset + lines.Length) (resultOffset + lines.Length))
                    else
                        sb.Add(sprintf "(End of buffer - total %d lines)" totalLines)
                    return frontMatterPrompt fields (String.concat "\n" sb)
                else
                    let ignoreCaseBool = if Dyn.isNullish (Dyn.get args "ignoreCase") then false else unbox<bool> args?ignoreCase
                    let flags = if ignoreCaseBool then "i" else ""
                    let regex = newRegex pattern flags
                    let result = mgr?search(id, regex, offset', limit')
                    if Dyn.isNullish result then failwithf "PTY session not found: %s" id
                    let matches : obj array = unbox (result?matches)
                    let totalLines = unbox<int> result?totalLines
                    let totalMatches = unbox<int> result?totalMatches
                    let hasMore = unbox<bool> result?hasMore
                    let fields = [
                        "id", box id
                        "status", box (string session?status)
                        "pattern", box pattern
                        "offset", box offset'
                        "returned", box matches.Length
                        "total_matches", box totalMatches
                        "total_lines", box totalLines
                        "has_more", box hasMore
                    ]
                    let sb = ResizeArray<string>()
                    if matches.Length = 0 then
                        sb.Add(sprintf "No lines matched the pattern '%s'." pattern)
                        sb.Add(sprintf "Total lines in buffer: %d" totalLines)
                    else
                        for i in 0 .. matches.Length - 1 do
                            let m = matches.[i]
                            sb.Add(string m?text)
                        sb.Add("")
                        if hasMore then
                            sb.Add(sprintf "(%d of %d matches shown. Use offset=%d to see more.)" matches.Length totalMatches (offset' + matches.Length))
                        else
                            sb.Add(sprintf "(%d match%s from %d total lines)" totalMatches (if totalMatches = 1 then "" else "es") totalLines)
                    return frontMatterPrompt fields (String.concat "\n" sb)
            })

let ptyListTool (host: Host) : obj =
    define "List all PTY sessions with status, PID, line count, and other metadata."
        (createObj [||])
        (fun _args context ->
            checkExecPerm host context
            promise {
                let! mgr = getManager ()
                let sessions : obj array = unbox (mgr?list())
                if sessions.Length = 0 then
                    return frontMatterPrompt [ "session_count", box 0 ] "No active PTY sessions."
                else
                    let sb = ResizeArray<string>()
                    for s in sessions do
                        sb.Add(sprintf "### %s" (string s?``id``))
                        sb.Add(sprintf "title: %s" (string s?title))
                        sb.Add(sprintf "command: %s %s" (string s?command) (String.concat " " (unbox<string array> s?args)))
                        sb.Add(sprintf "status: %s" (string s?status))
                        sb.Add(sprintf "pid: %d" (unbox<int> s?pid))
                        sb.Add(sprintf "lines: %d" (unbox<int> s?lineCount))
                        sb.Add("")
                    return frontMatterPrompt [ "session_count", box sessions.Length ] (String.concat "\n" sb)
            })
