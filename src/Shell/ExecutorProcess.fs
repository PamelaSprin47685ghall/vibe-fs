module VibeFs.Shell.ExecutorProcess

open Fable.Core
open Fable.Core.JsInterop

[<Emit("process.platform")>]
let private platform : string = jsNative
[<Import("spawn", "node:child_process")>]
let private spawn (cmd: string) (args: string array) (opts: obj) : obj = jsNative
[<Emit("process.kill($0, $1)")>]
let private processKill (pid: int) (signal: string) : unit = jsNative
[<Emit("({ ...process.env })")>]
let private env () : obj = jsNative

/// A minimal view of a spawned child process with stdio streams.
type SpawnedChild =
    abstract member stdout: obj with get
    abstract member stderr: obj with get
    abstract member on: event: string * handler: obj -> unit
    abstract member kill: signal: string -> bool

/// Kill a child process and its whole process tree.
let killTree (childProcess: obj) : unit =
    let pid = childProcess?("pid")
    if isNull pid then ()
    else
        let pidNum = unbox<int> pid
        try
            if platform = "win32" then
                spawn "taskkill" [| "/F"; "/T"; "/PID"; string pidNum |] (box {| stdio = "ignore" |}) |> ignore
            else processKill (-pidNum) "SIGKILL"
        with _ ->
            try ((childProcess :?> SpawnedChild).kill "SIGKILL") |> ignore with _ -> ()

/// Spawn a child with piped stdio, inheriting process.env, detached on Unix.
let spawnChild (command: string) (args: string array) (cwd: string) : SpawnedChild =
    let opts =
        box {| cwd = cwd
               env = env ()
               stdio = [| "ignore"; "pipe"; "pipe" |]
               detached = platform <> "win32"
               windowsHide = true |}
    spawn command args opts :?> SpawnedChild

let isWindows () : bool = platform = "win32"
