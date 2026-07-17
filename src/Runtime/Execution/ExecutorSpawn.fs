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

// --- helpers for awaitChild -------------------------------------------------

type private AwaitState =
    { child: SpawnedChild
      sessionId: string
      resolve: RunOutcome -> unit
      mutable settled: bool
      mutable totalBytes: int
      mutable limitReached: bool
      stdout: ResizeArray<string>
      stderr: ResizeArray<string>
      mutable onStdoutH: obj -> unit
      mutable onStderrH: obj -> unit
      mutable onErrorH: obj -> unit
      mutable onCloseH: System.Func<obj, obj, unit> }

let private settleOutcome (state: AwaitState) (outcome: RunOutcome) =
    if not state.settled then
        state.settled <- true
        // remove each handler by the exact delegate that was registered
        try
            state.child?stdout?removeListener ("data", state.onStdoutH) |> ignore
        with _ ->
            ()

        try
            state.child?stderr?removeListener ("data", state.onStderrH) |> ignore
        with _ ->
            ()

        try
            state.child?removeListener ("error", state.onErrorH) |> ignore
        with _ ->
            ()

        try
            state.child?removeListener ("close", state.onCloseH) |> ignore
        with _ ->
            ()

        unregisterActiveRun state.sessionId (fun () -> killTree state.child)
        state.resolve outcome

let private makeDataHandler (state: AwaitState) (buf: ResizeArray<string>) (otherBuf: ResizeArray<string>) =
    fun (c: obj) ->
        if not state.settled && not state.limitReached then
            let s = string c
            let len = s.Length

            if state.totalBytes + len > 2 * 1024 * 1024 then
                let remaining = 2 * 1024 * 1024 - state.totalBytes

                if remaining > 0 then
                    buf.Add(s.Substring(0, remaining))

                state.limitReached <- true
                killTree state.child
                settleOutcome state (Signaled("SIGKILL", String.concat "" buf, String.concat "" otherBuf))
            else
                buf.Add(s)
                state.totalBytes <- state.totalBytes + len

let private makeCloseHandler (state: AwaitState) =
    System.Func<obj, obj, unit>(fun code signal ->
        let capturedOut = String.concat "" state.stdout
        let capturedErr = String.concat "" state.stderr

        settleOutcome
            state
            (if state.limitReached then
                 Signaled("SIGKILL", capturedOut, capturedErr)
             elif isNull code then
                 let sigName = if isNull signal then "unknown" else string signal
                 Signaled(sigName, capturedOut, capturedErr)
             else
                 Exited(unbox<int> code, capturedOut, capturedErr)))

let private makeErrorHandler (state: AwaitState) (executable: string) =
    fun (_e: obj) -> settleOutcome state (SpawnFailed(ExecutorExecutableMissing executable))

let private registerSessionRun (state: AwaitState) (onKillRegistered: ((unit -> unit) -> unit) option) =
    match state.sessionId with
    | sid when sid <> "" ->
        registerActiveRun sid (fun () -> killTree state.child)

        onKillRegistered
        |> Option.iter (fun register -> register (fun () -> killTree state.child))
    | _ -> ()

// --- public API --------------------------------------------------------------

let private awaitChild
    (child: SpawnedChild)
    (executable: string)
    (kill: SpawnedChild -> unit)
    (timeoutMs: int option)
    (sessionId: string option)
    (onKillRegistered: ((unit -> unit) -> unit) option)
    : JS.Promise<RunOutcome> =
    Promise.create (fun resolve _reject ->
        let state =
            { child = child
              sessionId = defaultArg sessionId ""
              resolve = resolve
              settled = false
              totalBytes = 0
              limitReached = false
              stdout = ResizeArray<string>()
              stderr = ResizeArray<string>()
              onStdoutH = fun _ -> ()
              onStderrH = fun _ -> ()
              onErrorH = fun _ -> ()
              onCloseH = null }

        state.onStdoutH <- makeDataHandler state state.stdout state.stderr
        state.onStderrH <- makeDataHandler state state.stderr state.stdout
        state.onErrorH <- makeErrorHandler state executable
        state.onCloseH <- makeCloseHandler state

        registerSessionRun state onKillRegistered

        child?stdout?on ("data", state.onStdoutH) |> ignore
        child?stderr?on ("data", state.onStderrH) |> ignore
        child?on ("error", state.onErrorH) |> ignore
        child?on ("close", state.onCloseH) |> ignore

        match timeoutMs with
        | None -> ()
        | Some ms ->
            promise {
                do! Promise.sleep ms

                if not state.settled then
                    kill child
                    settleOutcome state (TimedOut(String.concat "" state.stdout, String.concat "" state.stderr))
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
