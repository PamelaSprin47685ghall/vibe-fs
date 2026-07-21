module Wanxiangshu.Hosts.Opencode.PtyReadTool

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolPermission
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Hosts.Opencode.PtySpawn
open Wanxiangshu.Hosts.Opencode.PtyReadOutput

module Dyn = Wanxiangshu.Runtime.Dyn

let private readPtyOutput
    (mgr: obj)
    (lifecycleManager: obj)
    (id: string)
    (sessionId: string)
    (pattern: string)
    (offset: int)
    (limit: int)
    (ignoreCase: bool)
    : JS.Promise<string> =
    promise {
        let sessionRaw = lifecycleManager?getSession (id)

        if Dyn.isNullish sessionRaw || string sessionRaw?parentSessionId <> sessionId then
            failwithf "PTY session not found: %s" id

        let session = lifecycleManager?toInfo (sessionRaw)

        if pattern = "" then
            return! readUnfiltered mgr id session offset limit
        else
            return! readFiltered mgr id session pattern offset limit ignoreCase
    }

let ptyReadTool (host: Host) : obj =
    define
        "Read output buffer from a PTY session with pagination (offset/limit) and optional regex pattern filtering."
        (createObj
            [ "id", box (strReq "The PTY session ID (e.g., pty_a1b2c3d4)")
              "offset",
              box (
                  numOpt
                      "Line number to start reading from (0-based, defaults to 0). When using pattern, this applies to filtered matches."
              )
              "limit",
              box (
                  numOpt
                      "Number of lines to read (defaults to 500). When using pattern, this applies to filtered matches."
              )
              "pattern",
              box (
                  strOpt
                      "Regex pattern to filter lines. When set, only matching lines are returned, then offset/limit apply to the matches."
              )
              "ignoreCase", box (boolOpt "Case-insensitive pattern matching (default: false)")
              "follow-tdd-and-kolmogorov-principles", box warnTddParam
              "impossible-via-other-tools", box warnParam ])
        (fun args context ->
            checkExecPerm host context
            let id = string args?``id``
            let sessionId = Dyn.str context "sessionID"

            let offset' =
                if Dyn.isNullish (Dyn.get args "offset") then
                    0
                else
                    unbox<int> args?offset

            let limit' =
                if Dyn.isNullish (Dyn.get args "limit") then
                    500
                else
                    unbox<int> args?limit

            let pattern = Dyn.str args "pattern"

            promise {
                let! mgr = getManager ()
                let lm = mgr?lifecycleManager

                let ignoreCaseBool =
                    if Dyn.isNullish (Dyn.get args "ignoreCase") then
                        false
                    else
                        unbox<bool> args?ignoreCase

                return! readPtyOutput mgr lm id sessionId pattern offset' limit' ignoreCaseBool
            })
