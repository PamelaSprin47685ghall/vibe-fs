module Wanxiangshu.Opencode.PtyTools

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolPermission
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Opencode.ToolSchema

module Dyn = Wanxiangshu.Shell.Dyn

[<Emit("import($0)")>]
let private dynImport (s: string) : JS.Promise<obj> = jsNative

[<Emit("new RegExp($0, $1)")>]
let private newRegex (pattern: string) (flags: string) : obj = jsNative

let private ptyClient : obj ref = ref (box null)
let private managerRef : obj ref = ref (box null)

let storePtyClient (client: obj) : unit = ptyClient := client

let private getManager () : JS.Promise<obj> =
    if not (Dyn.isNullish !managerRef) then promise { return !managerRef }
    else
        promise {
            let! mod' = dynImport "opencode-pty/plugin/pty/manager"
            if not (Dyn.isNullish !ptyClient) then
                try mod'?initManager(!ptyClient) with _ -> ()
            managerRef := mod'?manager
            return mod'?manager
        }

let cleanupPtyBySession (sessionId: string) : unit =
    promise {
        try
            let! mgr = getManager ()
            mgr?cleanupBySession(sessionId) |> ignore
        with _ -> ()
    } |> ignore

let private checkExecPerm (host: Host) (context: obj) : unit =
    let agent = Dyn.str context "agent"
    if not (canUseForHost host agent "executor") then
        failwithf "PTY tool denied: executor permission required, agent '%s' lacks it" agent

let private envFieldSchema : obj = strOpt "Additional environment variables (key-value object)"

let ptySpawnTool (host: Host) : obj =
    define "Create a new PTY session (pseudo-terminal) for running background processes, dev servers, watch modes, long-running commands. Returns session ID for use with other pty tools."
        (createObj [
            "command", box (strReq "The command/executable to run")
            "args", box (strArrayReq "Arguments to pass to the command")
            "workdir", box (strOpt "Working directory for the PTY session")
            "env", box envFieldSchema
            "title", box (strOpt "Human-readable title for the session")
            "description", box (strReq "Clear, concise description of what this PTY session is for in 5-10 words")
            "notifyOnExit", box (boolOpt "If true, sends a notification to the session when the process exits (default: false)")
            "timeoutSeconds", box (numOpt "Optional per-session timeout in seconds. The PTY is killed automatically when this duration elapses.")
        ])
        (fun args context ->
            checkExecPerm host context
            let sessionId = Dyn.str context "sessionID"
            let agent = Dyn.str context "agent"
            promise {
                let! mgr = getManager ()
                let info = mgr?spawn(createObj [
                    "command", args?command
                    "args", args?args
                    "workdir", args?workdir
                    "env", args?env
                    "title", args?title
                    "description", args?description
                    "parentSessionId", box sessionId
                    "parentAgent", box agent
                    "notifyOnExit", args?notifyOnExit
                    "timeoutSeconds", args?timeoutSeconds
                ])
                let lines = ResizeArray<string>()
                lines.Add("<pty_spawned>")
                lines.Add(sprintf "ID: %s" (string info?``id``))
                lines.Add(sprintf "Title: %s" (string info?title))
                lines.Add(sprintf "Command: %s %s" (string info?command) (String.concat " " (unbox<string array> info?args)))
                lines.Add(sprintf "Workdir: %s" (string info?workdir))
                lines.Add(sprintf "PID: %d" (unbox<int> info?pid))
                lines.Add(sprintf "Status: %s" (string info?status))
                lines.Add(sprintf "NotifyOnExit: %b" (unbox<bool> info?notifyOnExit))
                let timeoutStr = if Dyn.isNullish info?timeoutSeconds then "none" else string info?timeoutSeconds
                lines.Add(sprintf "TimeoutSeconds: %s" timeoutStr)
                lines.Add("</pty_spawned>")
                return String.concat "\n" lines
            })

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
                return sprintf "Sent %d bytes to %s: \"%s\"" data.Length id display
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
                    let sb = ResizeArray<string>()
                    sb.Add(sprintf "<pty_output id=\"%s\" status=\"%s\">" id (string session?status))
                    for i in 0 .. lines.Length - 1 do
                        sb.Add(lines.[i])
                    sb.Add("")
                    if hasMore then
                        sb.Add(sprintf "(Buffer has more lines. Use offset=%d to read beyond line %d)" (resultOffset + lines.Length) (resultOffset + lines.Length))
                    else
                        sb.Add(sprintf "(End of buffer - total %d lines)" totalLines)
                    sb.Add("</pty_output>")
                    return String.concat "\n" sb
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
                    let sb = ResizeArray<string>()
                    sb.Add(sprintf "<pty_output id=\"%s\" status=\"%s\" pattern=\"%s\">" id (string session?status) pattern)
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
                    sb.Add("</pty_output>")
                    return String.concat "\n" sb
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
                    return "<pty_list>\nNo active PTY sessions.\n</pty_list>"
                else
                    let sb = ResizeArray<string>()
                    sb.Add("<pty_list>")
                    for s in sessions do
                        sb.Add(sprintf "ID: %s" (string s?``id``))
                        sb.Add(sprintf "  Title: %s" (string s?title))
                        sb.Add(sprintf "  Command: %s %s" (string s?command) (String.concat " " (unbox<string array> s?args)))
                        sb.Add(sprintf "  Status: %s" (string s?status))
                        sb.Add(sprintf "  PID: %d" (unbox<int> s?pid))
                        sb.Add(sprintf "  Lines: %d" (unbox<int> s?lineCount))
                        sb.Add("")
                    sb.Add(sprintf "Total: %d session(s)" sessions.Length)
                    sb.Add("</pty_list>")
                    return String.concat "\n" sb
            })

let ptyKillTool (host: Host) : obj =
    define "Terminate a PTY session and optionally remove it from the session list (cleanup)."
        (createObj [
            "id", box (strReq "The PTY session ID (e.g., pty_a1b2c3d4)")
            "cleanup", box (boolOpt "If true, removes the session and frees the buffer (default: false)")
        ])
        (fun args context ->
            checkExecPerm host context
            let id = string args?``id``
            let cleanup = if Dyn.isNullish (Dyn.get args "cleanup") then false else unbox<bool> args?cleanup
            promise {
                let! mgr = getManager ()
                let session = mgr?``get``(id)
                if Dyn.isNullish session then failwithf "PTY session not found: %s" id
                let wasRunning = string session?status = "running"
                let success = unbox<bool> (mgr?kill(id, cleanup))
                if not success then failwithf "Failed to kill PTY session '%s'" id
                let action = if wasRunning then "Killed" else "Cleaned up"
                let cleanupNote = if cleanup then " (session removed)" else " (session retained for log access)"
                let lines = [|
                    "<pty_killed>"
                    sprintf "%s: %s%s" action id cleanupNote
                    sprintf "Title: %s" (string session?title)
                    sprintf "Command: %s %s" (string session?command) (String.concat " " (unbox<string array> session?args))
                    sprintf "Final line count: %d" (unbox<int> session?lineCount)
                    "</pty_killed>"
                |]
                return String.concat "\n" lines
            })
