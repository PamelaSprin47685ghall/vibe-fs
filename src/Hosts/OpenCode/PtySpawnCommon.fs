module Wanxiangshu.Hosts.Opencode.PtySpawnCommon

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

let envFieldSchema: obj =
    strOpt "Additional environment variables (key-value object)"

let spawnParamsSchema: obj =
    createObj
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
          )
          "follow-tdd-and-kolmogorov-principles", box warnTddParam
          "impossible-via-other-tools", box warnParam ]

let executeSpawn (mgr: obj) (args: obj) (sessionId: string) (agent: string) : obj =
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

let formatSpawnResponse (info: obj) : string =
    let timeoutStr =
        if Dyn.isNullish info?timeoutSeconds then
            "none"
        else
            string info?timeoutSeconds

    let fields =
        [ "id", box (string info?``id``)
          "title", box (string info?title)
          "command", box (sprintf "%s %s" (string info?command) (String.concat " " (unbox<string array> info?args)))
          "workdir", box (string info?workdir)
          "pid", box (unbox<int> info?pid)
          "status", box (string info?status)
          "notify_on_exit", box (unbox<bool> info?notifyOnExit)
          "timeout_seconds", box timeoutStr ]

    frontMatterPrompt fields "PTY session spawned."
