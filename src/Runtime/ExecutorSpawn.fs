module Wanxiangshu.Runtime.ExecutorSpawn

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Runtime.ExecutorFormat
open Wanxiangshu.Runtime.ExecutorJavascript
open Wanxiangshu.Runtime.SessionExecutor
open Wanxiangshu.Runtime.ExecutorPlatform

type RunOutcome =
    | Exited of code: int * stdout: string * stderr: string
    | TimedOut of stdout: string * stderr: string
    | Signaled of signal: string * stdout: string * stderr: string
    | SpawnFailed of reason: DomainError

// ARCHITECTURE_EXEMPT: split this 131-line function later
let private awaitChild
    (child: SpawnedChild)
    (executable: string)
    (kill: SpawnedChild -> unit)
    (timeoutMs: int option)
    (sessionId: string option)
    (onKillRegistered: ((unit -> unit) -> unit) option)
    : JS.Promise<RunOutcome> =
    // ARCHITECTURE_EXEMPT: split this 123-line function later
    Promise.create (fun resolve _reject ->
        let stdout = ResizeArray<string>()
        let stderr = ResizeArray<string>()
        let mutable settled = false
        let mutable totalBytes = 0
        let mutable limitReached = false
        let outputLimit = 2 * 1024 * 1024

        let doKill () = kill child

        // 持有 handler 引用供 removeListener 精确移除；避免 removeAllListeners 误伤其他潜在监听
        let mutable onStdoutH: (obj -> unit) = fun _ -> ()
        let mutable onStderrH: (obj -> unit) = fun _ -> ()
        let mutable onErrorH: (obj -> unit) = fun _ -> ()
        let mutable onCloseH: System.Func<obj, obj, unit> = null

        let removeListeners () =
            try
                child?stdout?removeListener ("data", onStdoutH) |> ignore
            with _ ->
                ()

            try
                child?stderr?removeListener ("data", onStderrH) |> ignore
            with _ ->
                ()

            try
                child?removeListener ("error", onErrorH) |> ignore
            with _ ->
                ()

            try
                child?removeListener ("close", onCloseH) |> ignore
            with _ ->
                ()

        let settle outcome =
            if not settled then
                settled <- true
                removeListeners ()
                unregisterActiveRun (defaultArg sessionId "") doKill
                resolve outcome

        onStdoutH <-
            fun (c: obj) ->
                if not settled && not limitReached then
                    let s = string c
                    let len = s.Length

                    if totalBytes + len > outputLimit then
                        let remaining = outputLimit - totalBytes

                        if remaining > 0 then
                            stdout.Add(s.Substring(0, remaining))

                        limitReached <- true
                        doKill ()
                        settle (Signaled("SIGKILL", String.concat "" stdout, String.concat "" stderr))
                    else
                        stdout.Add(s)
                        totalBytes <- totalBytes + len

        onStderrH <-
            fun (c: obj) ->
                if not settled && not limitReached then
                    let s = string c
                    let len = s.Length

                    if totalBytes + len > outputLimit then
                        let remaining = outputLimit - totalBytes

                        if remaining > 0 then
                            stderr.Add(s.Substring(0, remaining))

                        limitReached <- true
                        doKill ()
                        settle (Signaled("SIGKILL", String.concat "" stdout, String.concat "" stderr))
                    else
                        stderr.Add(s)
                        totalBytes <- totalBytes + len

        onErrorH <- fun (_e: obj) -> settle (SpawnFailed(ExecutorExecutableMissing executable))

        onCloseH <-
            System.Func<obj, obj, unit>(fun code signal ->
                let capturedOut = String.concat "" stdout
                let capturedErr = String.concat "" stderr

                settle (
                    if limitReached then
                        Signaled("SIGKILL", capturedOut, capturedErr)
                    elif isNull code then
                        let sigName = if isNull signal then "unknown" else string signal
                        Signaled(sigName, capturedOut, capturedErr)
                    else
                        Exited(unbox<int> code, capturedOut, capturedErr)
                ))

        let onTimeout () =
            if not settled then
                (doKill ()
                 settle (TimedOut(String.concat "" stdout, String.concat "" stderr)))

        match sessionId with
        | Some sid when sid <> "" ->
            registerActiveRun sid doKill
            onKillRegistered |> Option.iter (fun register -> register doKill)
        | _ -> ()

        child?stdout?on ("data", onStdoutH) |> ignore
        child?stderr?on ("data", onStderrH) |> ignore
        child?on ("error", onErrorH) |> ignore
        child?on ("close", onCloseH) |> ignore

        match timeoutMs with
        | None -> ()
        | Some ms ->
            promise {
                do! Promise.sleep ms
                onTimeout ()
            }
            |> Promise.start)

let spawnAndRun
    (command: string)
    (args: string array)
    (cwd: string)
    (timeoutMs: int option)
    (sessionId: string option)
    (onKillRegistered: ((unit -> unit) -> unit) option)
    : JS.Promise<RunOutcome> =
    let child = spawnChild command args cwd
    awaitChild child command killTree timeoutMs sessionId onKillRegistered

let runScript
    (interpreter: string)
    (interpreterArgs: string array)
    (cwd: string)
    (scriptPath: string)
    (timeoutMs: int option)
    (sessionId: string option)
    (onKillRegistered: ((unit -> unit) -> unit) option)
    : JS.Promise<RunOutcome> =
    spawnAndRun interpreter (Array.append interpreterArgs [| scriptPath |]) cwd timeoutMs sessionId onKillRegistered

let missingExecutableFor (language: ExecutorLanguage) : string =
    match language with
    | Python -> "uvx"
    | Javascript -> "npx"
    | Shell -> if isWindows () then "powershell.exe" else "bash"

let partialStdout (output: string) =
    if output = "" then
        "(no output before termination)"
    else
        output
