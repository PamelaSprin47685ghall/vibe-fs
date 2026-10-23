namespace Wanxiangshu.Process

open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Repository.Programming.Js

/// Host-owned process table for one programming call. A command starts
/// immediately and is addressed by an internal ticket while it runs.
/// Unread output is drained into an automatic park; the command's own
/// result is only the increment after the last drain.
module JsCallLoop =

    [<Import("spawn", "node:child_process")>]
    let private spawnProcess (command: string) (options: obj) : obj = jsNative

    [<Emit("Promise.resolve($0)")>]
    let private promiseOf (value: obj) : JS.Promise<obj> = jsNative

    /// Race one promise against a timeout and clear the timer when the
    /// promise wins, so a pending timer cannot keep the host event loop alive.
    [<Emit("(function(work, ms) { return new Promise(function(resolve) { let done = false; const timer = setTimeout(function() { if (!done) { done = true; resolve({ timeout: true }); } }, ms); Promise.resolve(work).then(function(value) { if (!done) { done = true; clearTimeout(timer); resolve({ value: value }); } }, function(error) { if (!done) { done = true; clearTimeout(timer); resolve({ error: String(error) }); } }); }); })($0, $1)")>]
    let private raceWorkTimeout (work: obj) (timeoutMs: int) : JS.Promise<obj> = jsNative

    [<Emit("$0.stdout.on('data', function(chunk) { $1(String(chunk)); })")>]
    let private onStdoutChunk (child: obj) (callback: string -> unit) : unit = jsNative

    [<Emit("$0.stderr.on('data', function(chunk) { $1(String(chunk)); })")>]
    let private onStderrChunk (child: obj) (callback: string -> unit) : unit = jsNative

    [<Emit("$0.on('exit', function(code) { $1(code === null || code === undefined ? null : code); })")>]
    let private onChildExit (child: obj) (callback: obj -> unit) : unit = jsNative

    [<Emit("$0.on('error', function(error) { $1(String((error && error.message) || error)); })")>]
    let private onChildError (child: obj) (callback: string -> unit) : unit = jsNative

    /// Kill the whole process group: a shell child carries its command as a
    /// grandchild, and signalling only the shell would orphan it.
    [<Emit("try { process.kill(-$0.pid, $1); } catch (error) { try { $0.kill($1); } catch (fallback) {} }")>]
    let private killChildGroup (child: obj) (signal: string) : unit = jsNative

    let private normalizePayload (text: string) : string =
        if text = "" || text.EndsWith "\n" then
            text
        else
            text + "\n"

    [<Emit("try { if ($0.stdout) { $0.stdout.destroy(); } } catch (error) {} try { if ($0.stderr) { $0.stderr.destroy(); } } catch (error) {}")>]
    let private releaseChildStreams (child: obj) : unit = jsNative

    [<Emit("try { if ($0.stdin) { $0.stdin.write($1); } } catch (error) {}")>]
    let private writeChildStdin (child: obj) (input: string) : unit = jsNative

    [<Emit("typeof $0 === 'function'")>]
    let private isFunction (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'string'")>]
    let private isString (value: obj) : bool = jsNative

    [<Emit("$0 === undefined || $0 === null")>]
    let private isNullish (value: obj) : bool = jsNative

    [<Emit("Object.prototype.hasOwnProperty.call($0, $1)")>]
    let private hasProperty (value: obj) (name: string) : bool = jsNative

    [<Emit("Buffer.byteLength($0, 'utf8')")>]
    let private utf8Bytes (text: string) : int = jsNative

    let private outputHeadLimit = 131072
    let private outputTailLimit = 131072
    let private signalGraceMs = 2000

    let private appendBoundedChunk (head: string) (tail: string) (chunk: string) : string * string =
        if head.Length >= outputHeadLimit then
            head, tail + chunk
        elif chunk.Length <= outputHeadLimit - head.Length then
            head + chunk, tail
        else
            let room = outputHeadLimit - head.Length
            head + chunk.Substring(0, room), tail + chunk.Substring room

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
            let newHead, newTail = appendBoundedChunk head tail chunk
            head <- newHead
            tail <- newTail

            if tail.Length > outputTailLimit then
                tail <- tail.Substring(tail.Length - outputTailLimit)

    type private SliceBuffer() =
        // DSL-MUTABLE: algorithm-scratch — unread incremental output of one child
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
        member val Stdout = OutputBuffer() with get
        member val Stderr = OutputBuffer() with get
        member val SliceStdout = SliceBuffer() with get
        member val SliceStderr = SliceBuffer() with get

    let private nowMs () =
        System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

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

        onChildError child (fun _reason ->
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

    let private resolveSliceWaitOutcome (managed: ManagedChild) (winner: obj) : bool =
        if hasProperty winner "timeout" then
            managed.Exited
        else
            true

    let private waitSlice (managed: ManagedChild) (sliceMs: int) : Task<bool> =
        if managed.Exited then
            Task.FromResult true
        else
            task {
                let! winner = unbox<Task<obj>> (raceWorkTimeout (box managed.Exit.Task) sliceMs)
                return resolveSliceWaitOutcome managed winner
            }

    let private awaitExit (managed: ManagedChild) (graceMs: int) : Task<unit> =
        task {
            // DSL-MUTABLE: algorithm-scratch — grace-window wait cursor
            let mutable waited = 0

            while not managed.Exited && waited < graceMs do
                let! _ = waitSlice managed (max 25 (min 250 graceMs))
                waited <- waited + 250
        }

    let private awaitForever (managed: ManagedChild) : Task<unit> =
        task {
            while not managed.Exited do
                let! _ = waitSlice managed 250
                ()
        }

    /// One programming call owns one table. Foreground commands are not
    /// entered: their group dies before `Call` returns. Named children, if a
    /// caller started any, die when the host is disposed.
    type Host(root: string) =
        let children = Dictionary<string, ManagedChild>()
        let nextTicket = ref 0
        let mutable parkedTicket: string = null

        member private _.TryGet(name: string) =
            match children.TryGetValue name with
            | true, managed -> Some managed
            | false, _ -> None

        interface System.IDisposable with
            member _.Dispose() =
                for entry in children.Values |> Seq.toArray do
                    killManaged entry "SIGKILL"

                children.Clear()

        member _.DisposeAsync() : Task<unit> =
            task {
                for entry in children.Values |> Seq.toArray do
                    killManaged entry "SIGKILL"
                    let! _ = awaitExit entry signalGraceMs
                    ()

                children.Clear()
            }

        member _.Start(command: obj) : obj =
            if isFunction command || not (isString command) then
                failure "CALL_COMMAND_UNSUPPORTED" "this.call expects a command string"
            else
                incr nextTicket
                let ticket = "c" + string !nextTicket
                children.[ticket] <- startChild root ticket (string command)
                parkedTicket <- ticket
                createObj [ "ok" ==> true; "ticket" ==> ticket ]

        member _.Wait(ticket: obj) : Task<obj> =
            let key = if isNullish ticket then "" else string ticket

            match children.TryGetValue key with
            | false, _ -> Task.FromResult(failure "CALL_CHILD_UNKNOWN" "call is no longer running")
            | true, managed ->
                task {
                    let started = nowMs ()
                    do! awaitForever managed
                    let stdout = managed.SliceStdout.Take()
                    let stderr = managed.SliceStderr.Take()
                    children.Remove key |> ignore
                    killManaged managed "SIGKILL"

                    return
                        outcome
                            "completed"
                            (Some managed.ExitCode)
                            stdout
                            stderr
                            (managed.Stdout.Truncated || managed.Stderr.Truncated)
                            (utf8Bytes stdout + utf8Bytes stderr)
                            (nowMs () - started)
                }

        member _.SignalParked(signal: obj) : Task<obj> =
            let key = if isNull parkedTicket then "" else parkedTicket

            let signalText =
                if isString signal && string signal <> "" then
                    string signal
                else
                    "SIGTERM"

            match children.TryGetValue key with
            | false, _ -> Task.FromResult(failure "CALL_CHILD_UNKNOWN" "signal needs the call that parked this program")
            | true, managed ->
                task {
                    killManaged managed signalText
                    return outcome "signalled" None "" "" false 0 0L
                }

        member _.SendParked(input: obj) : Task<obj> =
            task {
                let key = if isNull parkedTicket then "" else parkedTicket
                let text = if isNullish input then "" else string input
                let payload = normalizePayload text

                match children.TryGetValue key with
                | false, _ -> return failure "CALL_CHILD_UNKNOWN" "send needs the call that parked this program"
                | true, managed ->
                    writeChildStdin managed.Child payload
                    return outcome "sent" None "" "" false 0 0L
            }

        /// Unread stdout and stderr since the previous drain, every live call.
        member _.TakeDelta() : obj =
            let stdout = ResizeArray<string>()
            let stderr = ResizeArray<string>()

            for entry in children.Values do
                stdout.Add(entry.SliceStdout.Take())
                stderr.Add(entry.SliceStderr.Take())

            createObj [ "stdout" ==> String.concat "" stdout; "stderr" ==> String.concat "" stderr ]
