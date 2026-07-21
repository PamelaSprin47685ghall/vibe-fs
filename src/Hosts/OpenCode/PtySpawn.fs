module Wanxiangshu.Hosts.Opencode.PtySpawn

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolPermission
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Runtime.PromptHeader
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Hosts.Opencode

module Dyn = Wanxiangshu.Runtime.Dyn

let newRegex (pattern: string) (flags: string) : obj = PtySpawnCommon.newRegex pattern flags
let storePtyClient (client: obj) : unit = PtySpawnCommon.storePtyClient client
let getManager () : JS.Promise<obj> = PtySpawnCommon.getManager ()

let cleanupPtyBySession (sessionId: string) : unit =
    PtySpawnCommon.cleanupPtyBySession sessionId

let checkExecPerm (host: Host) (context: obj) : unit =
    PtySpawnCommon.checkExecPerm host context

let ptySpawnTool (host: Host) : obj =
    define
        "Create a new PTY session (pseudo-terminal) for running background processes, dev servers, watch modes, long-running commands. Returns session ID for use with other pty tools."
        PtySpawnCommon.spawnParamsSchema
        (fun args context ->
            checkExecPerm host context
            let sessionId = Dyn.str context "sessionID"
            let agent = Dyn.str context "agent"

            promise {
                let! mgr = getManager ()
                let info = PtySpawnCommon.executeSpawn mgr args sessionId agent
                return PtySpawnCommon.formatSpawnResponse info
            })

let formatSessionList (sessions: obj array) : string =
    let sb = ResizeArray<string>()

    for s in sessions do
        sb.Add(sprintf "### %s" (string s?``id``))
        sb.Add(sprintf "title: %s" (string s?title))

        sb.Add(sprintf "command: %s %s" (string s?command) (String.concat " " (unbox<string array> s?args)))

        sb.Add(sprintf "status: %s" (string s?status))
        sb.Add(sprintf "pid: %d" (unbox<int> s?pid))
        sb.Add(sprintf "lines: %d" (unbox<int> s?lineCount))
        sb.Add("")

    String.concat "\n" sb

let ptyKillTool (host: Host) : obj =
    define
        "Terminate a PTY session and optionally remove it from the session list (cleanup)."
        (createObj
            [ "id", box (strReq "The PTY session ID (e.g., pty_a1b2c3d4)")
              "cleanup", box (boolOpt "If true, removes the session and frees the buffer (default: false)")
              "follow-tdd-and-kolmogorov-principles", box warnTddParam
              "impossible-via-other-tools", box warnImpossibleViaOtherToolsParam ])
        (fun args context ->
            checkExecPerm host context
            let id = string args?``id``
            let sessionId = Dyn.str context "sessionID"

            let cleanup =
                if Dyn.isNullish (Dyn.get args "cleanup") then
                    false
                else
                    unbox<bool> args?cleanup

            promise {
                let! mgr = getManager ()
                let lm = mgr?lifecycleManager
                let sessionRaw = lm?getSession (id)

                if Dyn.isNullish sessionRaw || string sessionRaw?parentSessionId <> sessionId then
                    failwithf "PTY session not found: %s" id

                let session = lm?toInfo (sessionRaw)
                let wasRunning = string session?status = "running"
                let success = unbox<bool> (mgr?kill (id, cleanup))

                if not success then
                    failwithf "Failed to kill PTY session '%s'" id

                let action = if wasRunning then "killed" else "cleaned_up"

                let retainedNote =
                    if cleanup then
                        "session removed"
                    else
                        "session retained for log access"

                let fields =
                    [ "id", box id
                      "action", box action
                      "cleanup", box cleanup
                      "title", box (string session?title)
                      "command",
                      box (
                          sprintf
                              "%s %s"
                              (string session?command)
                              (String.concat " " (unbox<string array> session?args))
                      )
                      "final_line_count", box (unbox<int> session?lineCount) ]

                return frontMatterPrompt fields (sprintf "%s %s (%s)." action id retainedNote)
            })

let ptyListTool (host: Host) : obj =
    define
        "List all active PTY sessions."
        (createObj
            [ "follow-tdd-and-kolmogorov-principles", box warnTddParam
              "impossible-via-other-tools", box warnImpossibleViaOtherToolsParam ])
        (fun _ context ->
            checkExecPerm host context
            let sessionId = Dyn.str context "sessionID"

            promise {
                let! mgr = getManager ()
                let lm = mgr?lifecycleManager
                let sessionsRaw = lm?listSessions ()

                let sessions =
                    if Dyn.isNullish sessionsRaw then
                        [||]
                    else
                        unbox<obj array> sessionsRaw
                        |> Seq.filter (fun s -> string s?parentSessionId = sessionId)
                        |> Seq.map (fun s -> lm?toInfo (s))
                        |> Seq.toArray

                let body =
                    if sessions.Length = 0 then
                        "No active PTY sessions."
                    else
                        formatSessionList sessions

                return frontMatterPrompt [ "count", box sessions.Length ] body
            })
