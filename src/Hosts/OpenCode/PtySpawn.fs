module Wanxiangshu.Hosts.Opencode.PtySpawn

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolPermission
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Runtime.PromptFrontMatter
open Wanxiangshu.Hosts.Opencode.ToolSchema

module Dyn = Wanxiangshu.Runtime.Dyn

[<Emit("import($0)")>]
let dynImport (s: string) : JS.Promise<obj> = jsNative

[<Emit("new RegExp($0, $1)")>]
let newRegex (pattern: string) (flags: string) : obj = jsNative

let ptyClient: obj ref = ref (box null)
let managerRef: obj ref = ref (box null)

let storePtyClient (client: obj) : unit = ptyClient.Value <- client

let getManager () : JS.Promise<obj> =
    if not (Dyn.isNullish managerRef.Value) then
        promise { return managerRef.Value }
    else
        promise {
            let! mod' = dynImport "opencode-pty/plugin/pty/manager"

            if not (Dyn.isNullish ptyClient.Value) then
                try
                    mod'?initManager (ptyClient.Value)
                with _ ->
                    ()

            managerRef.Value <- mod'?manager
            return mod'?manager
        }

let cleanupPtyBySession (sessionId: string) : unit =
    promise {
        try
            let! mgr = getManager ()
            mgr?cleanupBySession (sessionId) |> ignore
        with _ ->
            ()
    }
    |> ignore

let checkExecPerm (host: Host) (context: obj) : unit =
    let agent = Dyn.str context "agent"

    if not (canUseForHost host agent "executor") then
        failwithf "PTY tool denied: executor permission required, agent '%s' lacks it" agent

let private envFieldSchema: obj =
    strOpt "Additional environment variables (key-value object)"

let ptySpawnTool (host: Host) : obj =
    define
        "Create a new PTY session (pseudo-terminal) for running background processes, dev servers, watch modes, long-running commands. Returns session ID for use with other pty tools."
        (createObj
            [ "command", box (strReq "The command/executable to run")
              "args", box (strArrayReq "Arguments to pass to the command")
              "workdir", box (strOpt "Working directory for the PTY session")
              "env", box envFieldSchema
              "title", box (strOpt "Human-readable title for the session")
              "description", box (strReq "Clear, concise description of what this PTY session is for in 5-10 words")
              "notifyOnExit",
              box (boolOpt "If true, sends a notification to the session when the process exits (default: false)")
              "timeoutSeconds",
              box (
                  numOpt
                      "Optional per-session timeout in seconds. The PTY is killed automatically when this duration elapses."
              ) ])
        (fun args context ->
            checkExecPerm host context
            let sessionId = Dyn.str context "sessionID"
            let agent = Dyn.str context "agent"

            promise {
                let! mgr = getManager ()

                let info =
                    mgr?spawn (
                        createObj
                            [ "command", args?command
                              "args", args?args
                              "workdir", args?workdir
                              "env", args?env
                              "title", args?title
                              "description", args?description
                              "parentSessionId", box sessionId
                              "parentAgent", box agent
                              "notifyOnExit", args?notifyOnExit
                              "timeoutSeconds", args?timeoutSeconds ]
                    )

                let timeoutStr =
                    if Dyn.isNullish info?timeoutSeconds then
                        "none"
                    else
                        string info?timeoutSeconds

                let fields =
                    [ "id", box (string info?``id``)
                      "title", box (string info?title)
                      "command",
                      box (sprintf "%s %s" (string info?command) (String.concat " " (unbox<string array> info?args)))
                      "workdir", box (string info?workdir)
                      "pid", box (unbox<int> info?pid)
                      "status", box (string info?status)
                      "notify_on_exit", box (unbox<bool> info?notifyOnExit)
                      "timeout_seconds", box timeoutStr ]

                return frontMatterPrompt fields "PTY session spawned."
            })

let ptyKillTool (host: Host) : obj =
    define
        "Terminate a PTY session and optionally remove it from the session list (cleanup)."
        (createObj
            [ "id", box (strReq "The PTY session ID (e.g., pty_a1b2c3d4)")
              "cleanup", box (boolOpt "If true, removes the session and frees the buffer (default: false)") ])
        (fun args context ->
            checkExecPerm host context
            let id = string args?``id``

            let cleanup =
                if Dyn.isNullish (Dyn.get args "cleanup") then
                    false
                else
                    unbox<bool> args?cleanup

            promise {
                let! mgr = getManager ()
                let session = mgr?``get`` (id)

                if Dyn.isNullish session then
                    failwithf "PTY session not found: %s" id

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
