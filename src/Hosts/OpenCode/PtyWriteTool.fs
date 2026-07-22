module Wanxiangshu.Hosts.Opencode.PtyWriteTool

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolPermission
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Runtime.Tooling.ToolOutputToml
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Hosts.Opencode.PtySpawn

open Wanxiangshu.Runtime.ToolOutputInfo

module Dyn = Wanxiangshu.Runtime.Dyn

let ptyWriteTool (host: Host) : obj =
    define
        "Send input to a PTY session. Supports escape sequences like \\x03 for Ctrl+C, \\n for newline, \\r for carriage return."
        (createObj
            [ "id", box (strReq "The PTY session ID (e.g., pty_a1b2c3d4)")
              "data", box (strReq "The input data to send to the PTY")
              "follow-tdd-and-kolmogorov-principles", box warnTddParam
              "impossible-via-other-tools", box warnImpossibleViaOtherToolsParam ])
        (fun args context ->
            checkExecPerm host context
            let id = string args?``id``
            let data = string args?data
            let sessionId = Dyn.str context "sessionID"

            promise {
                let! mgr = getManager ()
                let lm = mgr?lifecycleManager
                let sessionRaw = lm?getSession (id)

                if Dyn.isNullish sessionRaw || string sessionRaw?parentSessionId <> sessionId then
                    failwithf "PTY session not found: %s" id

                let success = unbox<bool> (mgr?write (id, data))

                if not success then
                    failwithf "Failed to write to PTY '%s'" id

                let preview =
                    if data.Length > 50 then
                        data.Substring(0, 50) + "..."
                    else
                        data

                let display =
                    preview
                        .Replace("\x03", "^C")
                        .Replace("\x04", "^D")
                        .Replace("\n", "\\n")
                        .Replace("\r", "\\r")

                let bodyText = sprintf "Sent: \"%s\"\nid: %s\nbytes: %d" display id data.Length

                let msg =
                    { empty with
                        status = Some "written"
                        body = Some bodyText }

                return renderToolOutput msg
            })
