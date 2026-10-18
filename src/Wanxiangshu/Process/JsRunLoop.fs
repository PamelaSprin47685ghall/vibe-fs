namespace Wanxiangshu.Process

open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Repository.Programming.Js

/// Host-owned execution loop for `js.run` (PLAN phase 2). The model writes one
/// Runnable class; this module owns the loop: it instantiates the class,
/// validates the Runnable spec, runs shell commands, calls `check` once per
/// time slice, dispatches `onExit`, enforces `maxDuration`, and returns one
/// structured outcome. A `check` verdict of `{ park: payload }` suspends
/// through the same park channel as `js.park`.
module JsRunLoop =

    [<Import("spawn", "node:child_process")>]
    let private spawnProcess (command: string) (options: obj) : obj = jsNative

    [<Import("connect", "node:net")>]
    let private netConnect: obj = jsNative

    [<Emit("(function(connectFn, port) { const socket = connectFn({ port: port, host: '127.0.0.1' }); return new Promise(function(resolve) { socket.once('connect', function() { socket.destroy(); resolve(true); }); socket.once('error', function() { resolve(false); }); }); })($0, $1)")>]
    let private probePort (connectFn: obj) (port: int) : JS.Promise<bool> = jsNative

    [<Emit("new Promise(function(resolve) { setTimeout(function() { resolve(undefined); }, $0); })")>]
    let private delay (milliseconds: int) : JS.Promise<unit> = jsNative

    /// Race one promise against a timeout and clear the timer when the
    /// promise wins: a pending slice timer would otherwise keep the host
    /// event loop alive after the run is already over.
    [<Emit("(function(work, ms) { return new Promise(function(resolve) { let done = false; const timer = setTimeout(function() { if (!done) { done = true; resolve({ timeout: true }); } }, ms); Promise.resolve(work).then(function(value) { if (!done) { done = true; clearTimeout(timer); resolve({ value: value }); } }, function(error) { if (!done) { done = true; clearTimeout(timer); resolve({ error: String(error) }); } }); }); })($0, $1)")>]
    let private raceWorkTimeout (work: obj) (timeoutMs: int) : JS.Promise<obj> = jsNative

    [<Emit("Promise.resolve($0)")>]
    let private promiseOf (value: obj) : JS.Promise<obj> = jsNative

    [<Emit("$0.stdout.on('data', function(chunk) { $1(String(chunk)); })")>]
    let private onStdoutChunk (child: obj) (callback: string -> unit) : unit = jsNative

    [<Emit("$0.stderr.on('data', function(chunk) { $1(String(chunk)); })")>]
    let private onStderrChunk (child: obj) (callback: string -> unit) : unit = jsNative

    [<Emit("$0.on('exit', function(code) { $1(code === null || code === undefined ? null : code); })")>]
    let private onChildExit (child: obj) (callback: obj -> unit) : unit = jsNative

    [<Emit("$0.on('error', function(error) { $1(String((error && error.message) || error)); })")>]
    let private onChildError (child: obj) (callback: string -> unit) : unit = jsNative


    /// Kill the whole process group: a shell child carries its command as a
    /// grandchild, and signalling only the shell would orphan it with the
    /// inherited stdio pipes.
    [<Emit("try { process.kill(-$0.pid, $1); } catch (error) { try { $0.kill($1); } catch (fallback) {} }")>]
    let private killChildGroup (child: obj) (signal: string) : unit = jsNative

    /// Drop the pipe handles after a kill so an exited child cannot keep the
    /// host event loop alive through an unread stdio stream.
    [<Emit("try { if ($0.stdout) { $0.stdout.destroy(); } } catch (error) {} try { if ($0.stderr) { $0.stderr.destroy(); } } catch (error) {}")>]
    let private releaseChildStreams (child: obj) : unit = jsNative

    /// Write to a spawned child's stdin; interactive use is `{ send, name, input }`.
    [<Emit("try { if ($0.stdin) { $0.stdin.write($1); } } catch (error) {}")>]
    let private writeChildStdin (child: obj) (input: string) : unit = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : obj = jsNative

    [<Emit("typeof $0 === 'function'")>]
    let private isFunction (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'string'")>]
    let private isString (value: obj) : bool = jsNative

    [<Emit("typeof $0")>]
    let private typeOf (value: obj) : string = jsNative

    [<Emit("$0 === undefined || $0 === null")>]
    let private isNullish (value: obj) : bool = jsNative

    [<Emit("Number.isFinite($0)")>]
    let private isFiniteNumber (value: obj) : bool = jsNative

    [<Emit("$0[$1]")>]
    let private getProperty (value: obj) (name: string) : obj = jsNative

    [<Emit("Object.prototype.hasOwnProperty.call($0, $1)")>]
    let private hasProperty (value: obj) (name: string) : bool = jsNative

    [<Emit("$0.next($1)")>]
    let private callNext (instance: obj) (ctx: obj) : obj = jsNative

    [<Emit("$0.check($1)")>]
    let private callCheck (instance: obj) (ctx: obj) : obj = jsNative

    [<Emit("$0.onExit($1)")>]
    let private callOnExit (instance: obj) (result: obj) : obj = jsNative

    [<Emit("Buffer.byteLength($0, 'utf8')")>]
    let private utf8Bytes (text: string) : int = jsNative

    let private outputHeadLimit = 131072
    let private outputTailLimit = 131072
    let private defaultIntervalMs = 10000
    let private minimumIntervalMs = 10
    let private signalGraceMs = 2000
    let private waitForDefaultMs = 30000
    let private probeIntervalMs = 100

    type private OutputBuffer() =
        // DSL-MUTABLE: algorithm-scratch — bounded head/tail capture for one stream
        let mutable head = ""
        let mutable tail = ""
        let mutable totalBytes = 0

        member _.TotalBytes = totalBytes

        member _.Truncated = totalBytes > head.Length + tail.Length

        member _.Text = head + tail

        member _.Append(chunk: string) =
            totalBytes <- totalBytes + utf8Bytes chunk

            if head.Length < outputHeadLimit then
                let room = outputHeadLimit - head.Length

                if chunk.Length <= room then
                    head <- head + chunk
                else
                    head <- head + chunk.Substring(0, room)
                    tail <- tail + chunk.Substring room
            else
                tail <- tail + chunk

            if tail.Length > outputTailLimit then
                tail <- tail.Substring(tail.Length - outputTailLimit)

    type private SliceBuffer() =
        // DSL-MUTABLE: algorithm-scratch — slice delta buffer drained by check
        let mutable text = ""

        member _.Append(chunk: string) = text <- text + chunk

        member _.Take() =
            let taken = text
            text <- ""
            taken

    type private ManagedChild(name: string, child: obj) =
        member _.Name = name
        member _.Child = child
        member val Exit = TaskCompletionSource<int>() with get
        member val Exited = false with get, set
        member val ExitCode: int = 0 with get, set
        member val Failed = false with get, set
        member val Stdout = OutputBuffer() with get
        member val Stderr = OutputBuffer() with get
        member val SliceStdout = SliceBuffer() with get
        member val SliceStderr = SliceBuffer() with get

    let private nowMs () =
        System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

    /// Fable's TaskCompletionSource does not expose TrySet*; a guarded
    /// SetResult is idempotent after the first completion.
    let private trySetResult (completion: TaskCompletionSource<int>) (value: int) =
        try
            completion.SetResult value
            true
        with _ ->
            false

    let private failure (code: string) (reason: string) : obj =
        createObj [ "ok" ==> false; "code" ==> code; "reason" ==> reason ]

    let private outcome
        (status: string)
        (exitCode: int option)
        (stdout: string)
        (stderr: string)
        (truncated: bool)
        (bytes: int)
        (durationMs: int64)
        : obj =
        createObj
            [ "ok" ==> true
              "status" ==> status
              "exitCode"
              ==> (match exitCode with
                   | Some code -> box code
                   | None -> null)
              "stdout" ==> stdout
              "stderr" ==> stderr
              "truncated" ==> truncated
              "bytes" ==> bytes
              "durationMs" ==> float durationMs ]

    let private awaitJs (value: obj) : Task<obj> = unbox<Task<obj>> (promiseOf value)

    let private startChild (root: string) (name: string) (command: string) : ManagedChild =
        let child =
            spawnProcess
                command
                (createObj
                    [ "shell" ==> true
                      "detached" ==> true
                      "cwd" ==> root
                      "stdio" ==> [| "pipe"; "pipe"; "pipe" |] ])

        let managed = ManagedChild(name, child)

        onStdoutChunk child (fun chunk ->
            managed.Stdout.Append chunk
            managed.SliceStdout.Append chunk)

        onStderrChunk child (fun chunk ->
            managed.Stderr.Append chunk
            managed.SliceStderr.Append chunk)

        onChildExit child (fun code ->
            managed.Exited <- true

            if not (isNullish code) then
                managed.ExitCode <- int (unbox<float> code)

            trySetResult managed.Exit managed.ExitCode |> ignore)

        onChildError child (fun reason ->
            managed.Failed <- true
            managed.Exited <- true
            managed.ExitCode <- -1
            trySetResult managed.Exit -1 |> ignore)

        managed

    let private killManaged (managed: ManagedChild) (signal: string) =
        try
            killChildGroup managed.Child signal
        with _ ->
            ()

        releaseChildStreams managed.Child

    let private childView (managed: ManagedChild) : obj =
        createObj
            [ "name" ==> managed.Name
              "exited" ==> managed.Exited
              "exitCode" ==> (if managed.Exited then box managed.ExitCode else null) ]

    /// Wait until the child exits or one time slice elapses. Returns true when
    /// the child has exited.
    let private waitSlice (managed: ManagedChild) (sliceMs: int) : Task<bool> =
        task {
            if managed.Exited then
                return true
            else
                let! winner = unbox<Task<obj>> (raceWorkTimeout (box managed.Exit.Task) sliceMs)

                if hasProperty winner "timeout" then
                    return managed.Exited
                else
                    return true
        }

    /// Await exit after a kill, up to the grace window.
    let private awaitExit (managed: ManagedChild) (graceMs: int) : Task<unit> =
        task {
            // DSL-MUTABLE: algorithm-scratch — grace-window wait cursor
            let mutable waited = 0

            while not managed.Exited && waited < graceMs do
                let! _ = waitSlice managed (max 25 (min 250 graceMs))
                waited <- waited + 250
        }

    /// Race one host call against the remaining run budget. `None` means the
    /// budget expired first.
    let private raceDeadline (work: Task<obj>) (timeoutMs: int) : Task<obj option> =
        task {
            if timeoutMs <= 0 then
                return None
            else
                let! winner = unbox<Task<obj>> (raceWorkTimeout (box work) timeoutMs)

                if hasProperty winner "timeout" then
                    return None
                elif hasProperty winner "error" then
                    return raise (System.Exception(string (getProperty winner "error")))
                else
                    return Some(getProperty winner "value")
        }

    type private Verdict =
        | Continue
        | Abort of string
        | Park of obj

    let private verdictOf (raw: obj) : Verdict =
        if isNullish raw then
            Continue
        elif typeOf raw = "boolean" then
            Continue
        elif hasProperty raw "abort" && not (isNullish (getProperty raw "abort")) then
            Abort(string (getProperty raw "abort"))
        elif hasProperty raw "park" then
            Park(getProperty raw "park")
        else
            Continue

    /// Suspend the run through the shared park channel and wait for the
    /// injected value. The injected string 'abort' aborts; anything else
    /// resumes the loop. Returns "unsupported" when the run has no park state.
    let private parkAtCheck
        (parkState: Wanxiangshu.Process.JsSandbox.ParkState option)
        (stack: string)
        (payload: obj)
        : Task<string> =
        task {
            match parkState with
            | None -> return "unsupported"
            | Some state ->
                state.ParkRequested <- true
                state.ParkPayload <- payload

                if not (isNull stack) then
                    state.ParkStack <- stack

                if not (isNull state.ParkSignalResolve) then
                    emitJsExpr (state.ParkSignalResolve, payload) "$0($1)" |> ignore

                let deferred: obj =
                    emitJsExpr
                        state
                        "(function(state) { return new Promise(function(resolve, reject) { state.ParkDeferredResolve = resolve; state.ParkDeferredReject = reject; }); })($0)"

                let! injected = awaitJs deferred

                if isString injected && string injected = "abort" then
                    return "abort"
                else
                    return "continue"
        }

    /// Execute one Runnable class. `root` is the shell working directory;
    /// `stack` is the model stack captured at the `js.run(...)` call site so a
    /// check-park carries the same execution marker as a direct park.
    let execute
        (parkState: Wanxiangshu.Process.JsSandbox.ParkState option)
        (root: string)
        (runnableClass: obj)
        (stack: string)
        : Task<obj> =
        task {
            if not (isFunction runnableClass) then
                return failure "RUNNABLE_SPEC_INCOMPLETE" "js.run expects a class extending js.Runnable"
            else
                let instance = construct runnableClass
                let maxDurationValue = getProperty instance "maxDuration"
                let hasNext = isFunction (getProperty instance "next")
                let hasCheck = isFunction (getProperty instance "check")

                if
                    isNullish maxDurationValue
                    || not (isFiniteNumber maxDurationValue)
                    || unbox<float> maxDurationValue <= 0.0
                then
                    return failure "RUNNABLE_SPEC_INCOMPLETE" "Runnable must declare a positive maxDuration"
                elif not hasNext then
                    return failure "RUNNABLE_SPEC_INCOMPLETE" "Runnable must implement next(ctx)"
                elif not hasCheck then
                    return failure "RUNNABLE_SPEC_INCOMPLETE" "Runnable must implement check(ctx)"
                else
                    let maxDurationMs = int64 (unbox<float> maxDurationValue)
                    let intervalValue = getProperty instance "interval"

                    let intervalMs =
                        if isNullish intervalValue || not (isFiniteNumber intervalValue) then
                            defaultIntervalMs
                        else
                            max minimumIntervalMs (int (unbox<float> intervalValue))

                    let hasOnExit = isFunction (getProperty instance "onExit")
                    let startedMs = nowMs ()
                    let deadlineMs = startedMs + maxDurationMs
                    let children = Dictionary<string, ManagedChild>()
                    let output = OutputBuffer()
                    // DSL-MUTABLE: algorithm-scratch — loop step cursor
                    let mutable step = 0
                    // DSL-MUTABLE: algorithm-scratch — overall loop state
                    let mutable running = true
                    let mutable status = "completed"
                    let mutable lastExitCode: int option = None
                    // DSL-MUTABLE: algorithm-scratch — failure/outcome slot for this run
                    let mutable result: obj = null
                    let scratch = ManagedChild("wait", null)

                    let checkContext (managed: ManagedChild) : obj =
                        // The slice deltas of the foreground command and every
                        // background child land in one observation window, so a
                        // spawned REPL's replies are visible from check.
                        let stdoutParts = ResizeArray<string>()
                        let stderrParts = ResizeArray<string>()
                        stdoutParts.Add(managed.SliceStdout.Take())
                        stderrParts.Add(managed.SliceStderr.Take())

                        for entry in children.Values |> Seq.sortBy (fun child -> child.Name) do
                            stdoutParts.Add(entry.SliceStdout.Take())
                            stderrParts.Add(entry.SliceStderr.Take())

                        createObj
                            [ "root" ==> root
                              "stdout" ==> String.concat "" stdoutParts
                              "stderr" ==> String.concat "" stderrParts
                              "exited" ==> managed.Exited
                              "exitCode" ==> (if managed.Exited then box managed.ExitCode else null)
                              "backgrounds" ==> (children.Values |> Seq.map childView |> Seq.toArray)
                              "elapsedMs" ==> float (nowMs () - startedMs)
                              "remainingMs" ==> float (max 0L (deadlineMs - nowMs ())) ]

                    let exitContext (managed: ManagedChild) : obj =
                        createObj
                            [ "root" ==> root
                              "exitCode" ==> (if managed.Exited then box managed.ExitCode else null)
                              "stdout" ==> managed.Stdout.Text
                              "stderr" ==> managed.Stderr.Text
                              "elapsedMs" ==> float (nowMs () - startedMs) ]

                    let parkOrAbort (payload: obj) : Task<string> =
                        task {
                            if isNull result then
                                let! decision = parkAtCheck parkState stack payload

                                match decision with
                                | "unsupported" ->
                                    result <-
                                        failure
                                            "RUN_PARK_UNSUPPORTED"
                                            "park verdict requires the orchestrated execution path"

                                    return "unsupported"
                                | other -> return other
                            else
                                return "stop"
                        }

                    let rec runForeground (command: string) : Task<unit> =
                        task {
                            let managed = startChild root "foreground" command
                            // DSL-MUTABLE: algorithm-scratch — per-command loop state
                            let mutable finished = false
                            let mutable aborted = false

                            while not finished && isNull result do
                                let remaining = deadlineMs - nowMs ()

                                if remaining <= 0L then
                                    status <- "timeout"
                                    killManaged managed "SIGKILL"
                                    do! finishManaged managed
                                    finished <- true
                                else
                                    let slice = int (min (int64 intervalMs) remaining)
                                    let! exited = waitSlice managed slice

                                    if exited then
                                        finished <- true
                                    else
                                        let! verdictRaw = awaitJs (callCheck instance (checkContext managed))

                                        match verdictOf verdictRaw with
                                        | Continue -> ()
                                        | Abort _ ->
                                            status <- "aborted"
                                            aborted <- true
                                            killManaged managed "SIGKILL"
                                            do! finishManaged managed
                                            finished <- true
                                        | Park payload ->
                                            let! decision = parkOrAbort payload

                                            match decision with
                                            | "abort" ->
                                                status <- "aborted"
                                                aborted <- true
                                                killManaged managed "SIGKILL"
                                                do! finishManaged managed
                                                finished <- true
                                            | "unsupported" ->
                                                killManaged managed "SIGKILL"
                                                do! finishManaged managed
                                                finished <- true
                                            | _ -> ()

                            if isNull result then
                                lastExitCode <- Some managed.ExitCode
                                output.Append managed.Stdout.Text
                                output.Append managed.Stderr.Text

                                if hasOnExit then
                                    let! _ = awaitJs (callOnExit instance (exitContext managed))
                                    ()

                            if aborted || status = "timeout" then
                                running <- false
                        }

                    and finishManaged (managed: ManagedChild) =
                        task {
                            if not managed.Exited then
                                let! _ = awaitExit managed signalGraceMs
                                ()
                        }

                    while running && isNull result do
                        let ctx = createObj [ "root" ==> root; "step" ==> step ]

                        let remaining = deadlineMs - nowMs ()
                        let! stepOutcome = raceDeadline (awaitJs (callNext instance ctx)) (int remaining)

                        let raw =
                            match stepOutcome with
                            | Some value -> value
                            | None -> null

                        if stepOutcome.IsNone then
                            status <- "timeout"
                            running <- false

                        step <- step + 1

                        if stepOutcome.IsNone then
                            ()
                        elif isNullish raw then
                            running <- false
                        elif isString raw then
                            do! runForeground (string raw)
                        elif hasProperty raw "spawn" then
                            let name = string (getProperty raw "name")
                            let command = string (getProperty raw "spawn")
                            children.[name] <- startChild root name command
                        elif hasProperty raw "signal" then
                            let name = string (getProperty raw "name")

                            let signalName =
                                let value = getProperty raw "signal"

                                if isString value && string value <> "" then
                                    string value
                                else
                                    "SIGTERM"

                            match children.TryGetValue name with
                            | true, managed ->
                                killManaged managed signalName
                                let! _ = awaitExit managed signalGraceMs

                                if not managed.Exited then
                                    killManaged managed "SIGKILL"
                                    let! _ = awaitExit managed signalGraceMs
                                    ()

                                children.Remove name |> ignore
                            | false, _ ->
                                result <- failure "RUN_CHILD_UNKNOWN" ("signal names an unknown background: " + name)
                        elif hasProperty raw "send" then
                            // Interactive write to a spawned child's stdin. A
                            // missing trailing newline is added (terminal
                            // convention); the child's incremental output
                            // arrives through the next check slice.
                            let send = getProperty raw "send"
                            let name = string (getProperty send "name")
                            let inputValue = getProperty send "input"

                            let input =
                                if isNullish inputValue then "" else string inputValue

                            match children.TryGetValue name with
                            | true, managed ->
                                let payload =
                                    if input = "" || input.EndsWith "\n" then
                                        input
                                    else
                                        input + "\n"

                                writeChildStdin managed.Child payload
                            | false, _ ->
                                result <- failure "RUN_CHILD_UNKNOWN" ("send names an unknown background: " + name)
                        elif hasProperty raw "waitFor" then
                            let waitFor = getProperty raw "waitFor"
                            let port = int (unbox<float> (getProperty waitFor "port"))

                            let timeoutMs =
                                let value = getProperty waitFor "timeoutMs"

                                if isNullish value || not (isFiniteNumber value) then
                                    waitForDefaultMs
                                else
                                    int (unbox<float> value)

                            let waitStarted = nowMs ()
                            // DSL-MUTABLE: algorithm-scratch — waitFor poll state
                            let mutable ready = false
                            let mutable failureReason: string option = None

                            while not ready && failureReason.IsNone && isNull result do
                                let! connected = unbox<Task<bool>> (probePort netConnect port)

                                if connected then
                                    ready <- true
                                elif nowMs () - waitStarted >= int64 timeoutMs then
                                    failureReason <- Some "timeout"
                                elif nowMs () >= deadlineMs then
                                    status <- "timeout"
                                    failureReason <- Some "deadline"
                                else
                                    let slice = int (min (int64 intervalMs) (max 1L (deadlineMs - nowMs ())))
                                    let! _ = unbox<Task<unit>> (delay (min slice probeIntervalMs))
                                    let! verdictRaw = awaitJs (callCheck instance (checkContext scratch))

                                    match verdictOf verdictRaw with
                                    | Continue -> ()
                                    | Abort _ -> failureReason <- Some "abort"
                                    | Park payload ->
                                        let! decision = parkOrAbort payload

                                        if decision = "abort" then
                                            failureReason <- Some "abort"
                                        elif decision = "unsupported" then
                                            failureReason <- Some "unsupported"

                            match failureReason with
                            | Some "timeout" when isNull result ->
                                result <- failure "RUN_WAIT_TIMEOUT" "waitFor condition was not met before its timeout"
                            | Some "deadline" -> running <- false
                            | Some "abort" ->
                                status <- "aborted"
                                running <- false
                            | Some "unsupported" -> running <- false
                            | _ -> ()
                        else
                            result <-
                                failure
                                    "RUN_COMMAND_UNSUPPORTED"
                                    "next() must return a command string, null, { spawn, name }, { send: { name, input } }, { signal, name } or { waitFor: { port, timeoutMs } }"

                    for entry in children.Values |> Seq.toArray do
                        killManaged entry "SIGKILL"
                        let! _ = awaitExit entry signalGraceMs
                        ()

                    if not (isNull result) then
                        return result
                    else
                        return
                            outcome
                                status
                                lastExitCode
                                output.Text
                                ""
                                output.Truncated
                                output.TotalBytes
                                (nowMs () - startedMs)
        }
