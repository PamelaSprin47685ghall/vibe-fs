namespace Wanxiangshu.Next.Process

open System
open System.Text
open System.Threading
open System.Threading.Tasks

[<RequireQualifiedAccess>]
type RunnerOutcome =
    | Completed of exitCode: int * stdout: string * stderr: string * spooled: bool
    | Spooled of exitCode: int * spoolPath: string * totalBytes: int64 * chunkCount: int * chunks: byte[][]
    | OutputExceeded of bytesWritten: int64 * spoolPath: string option

[<RequireQualifiedAccess>]
type RunnerError =
    | TimeoutExceeded of duration: TimeSpan
    | SpawnFailed of reason: string
    | ProcessCancelled of reason: string
    | ExecutionFailed of reason: string

module RunnerCore =

    open Fable.Core
    open Fable.Core.JsInterop

    [<Import("spawn", "node:child_process")>]
    let private spawnChildProcess (command: string) (args: string array) (options: obj) : obj = jsNative

    [<Emit("((child, text) => { try { if (child && child.stdin) { child.stdin.write(text, 'utf8'); child.stdin.end(); } } catch (_) {} return undefined; })($0, $1)")>]
    let private writeStdin (child: obj) (text: string) : unit = jsNative

    [<Emit("((child) => { try { if (child && child.stdin) child.stdin.end(); } catch (_) {} return undefined; })($0)")>]
    let private closeStdin (child: obj) : unit = jsNative

    let mutable private gatePromise: JS.Promise<unit> =
        JS.Constructors.Promise.resolve ()

    let mutable private releaseCurrent: (unit -> unit) option = None

    let getLargeGateCount () : int = if releaseCurrent.IsNone then 1 else 0

    let acquireLargeGate (_ct: CancellationToken) : Task =
        let mutable res = ignore
        let p = JS.Constructors.Promise.Create(fun resolve _ -> res <- resolve)
        let prev = gatePromise
        gatePromise <- p
        let tcs = TaskCompletionSource<unit>()

        emitJsExpr
            (prev,
             (fun () ->
                 releaseCurrent <- Some res
                 tcs.SetResult(())))
            "$0.then($1)"
        |> ignore

        tcs.Task

    let releaseLargeGate () : unit =
        match releaseCurrent with
        | Some r ->
            releaseCurrent <- None
            r ()
        | None -> ()

    let calculateDeadline (now: DateTimeOffset) (RuntimeSeconds estSecs: EstimatedRuntime) : Deadline =
        let budgetSecs = 3.0 * estSecs
        Deadline.ofBudget now (TimeSpan.FromSeconds budgetSecs)

    let killProcessGroup (child: obj) : unit =
        emitJsExpr
            child
            """
            try {
                if ($0 && $0.pid) {
                    process.kill(-$0.pid, 'SIGKILL');
                }
            } catch (_) {
                try { if ($0 && typeof $0.kill === 'function') $0.kill('SIGKILL'); } catch (_) {}
            }
        """
        |> ignore

    /// Core execution using system Process or Node child_process.
    let execute
        (cmd: Command)
        (estimate: ProcessEstimate)
        (ctx: ProcessContext)
        (ct: CancellationToken)
        : Task<Result<RunnerOutcome, RunnerError>> =
        task {
            let (RuntimeSeconds estSecs) = estimate.EstimatedRuntime

            let budgetSpan = TimeSpan.FromSeconds(3.0 * estSecs)
            let deadline = calculateDeadline DateTimeOffset.UtcNow estimate.EstimatedRuntime

            let isLarge = estimate.EstimatedMemory = EstimatedMemory.Large

            if isLarge then
                do! acquireLargeGate ct

            try
                try
                    if ct.IsCancellationRequested then
                        return Error(RunnerError.ProcessCancelled "Cancelled before spawn")
                    else
                        let jsEnv = emitJsExpr () "Object.assign({}, process.env)"

                        match cmd.Environment with
                        | Some envMap ->
                            for KeyValue(k, v) in envMap do
                                emitJsExpr (jsEnv, k, v) "$0[$1] = $2" |> ignore
                        | None -> ()

                        let cwdOpt =
                            match cmd.WorkingDirectory with
                            | Some wd -> Some wd
                            | None -> ctx.WorkingDirectory

                        let jsOptions =
                            match cwdOpt with
                            | Some wd ->
                                createObj
                                    [ "cwd" ==> wd
                                      "env" ==> jsEnv
                                      "detached" ==> true
                                      "stdio" ==> [| "pipe"; "pipe"; "pipe" |] ]
                            | None ->
                                createObj
                                    [ "env" ==> jsEnv
                                      "detached" ==> true
                                      "stdio" ==> [| "pipe"; "pipe"; "pipe" |] ]

                        let argsArray = cmd.Arguments |> List.toArray

                        let child =
                            try
                                spawnChildProcess cmd.FileName argsArray jsOptions
                            with _ ->
                                null

                        if isNull child then
                            return Error(RunnerError.SpawnFailed("Failed to spawn process: " + cmd.FileName))
                        else
                            let stdoutChunks = ResizeArray<obj>()
                            let stderrChunks = ResizeArray<obj>()

                            emitJsExpr
                                (child, stdoutChunks)
                                """
                                if ($0 && $0.stdout) {
                                    $0.stdout.on('data', function(chunk) {
                                        $1.push(chunk);
                                    });
                                }
                            """
                            |> ignore

                            emitJsExpr
                                (child, stderrChunks)
                                """
                                if ($0 && $0.stderr) {
                                    $0.stderr.on('data', function(chunk) {
                                        $1.push(chunk);
                                    });
                                }
                            """
                            |> ignore

                            match cmd.Stdin with
                            | Some stdinText -> writeStdin child stdinText
                            | None -> closeStdin child

                            let remMs =
                                Math.Max(
                                    0,
                                    int
                                        (Deadline.remaining (fun () -> DateTimeOffset.UtcNow) deadline)
                                            .TotalMilliseconds
                                )

                            let tcs = TaskCompletionSource<int * byte[] * byte[] * bool>()
                            let mutable finished = false
                            let mutable timerId: obj = null

                            let finish (code: int) (tOut: bool) =
                                if not finished then
                                    finished <- true

                                    if not (isNull timerId) then
                                        emitJsExpr timerId "clearTimeout($0)" |> ignore

                                    let outBuf = emitJsExpr stdoutChunks "Buffer.concat($0)"
                                    let errBuf = emitJsExpr stderrChunks "Buffer.concat($0)"

                                    let outBytes =
                                        emitJsExpr outBuf "new Uint8Array($0.buffer, $0.byteOffset, $0.byteLength)"
                                        |> unbox<byte[]>

                                    let errBytes =
                                        emitJsExpr errBuf "new Uint8Array($0.buffer, $0.byteOffset, $0.byteLength)"
                                        |> unbox<byte[]>

                                    tcs.SetResult(code, outBytes, errBytes, tOut)

                            timerId <-
                                emitJsExpr
                                    (remMs,
                                     (fun () ->
                                         killProcessGroup child
                                         finish -1 true))
                                    "setTimeout($1, $0)"

                            emitJsExpr
                                (child, (fun code -> finish code false), (fun _err -> finish -1 false))
                                """
                                if ($0) {
                                    $0.on('close', function(code) {
                                        $1(typeof code === 'number' ? code : 0);
                                    });
                                    $0.on('error', function(err) {
                                        $2(err);
                                    });
                                }
                            """
                            |> ignore

                            let! (exitCode, stdoutBytes, stderrBytes, timedOut) = tcs.Task

                            if timedOut || Deadline.isExpired (fun () -> DateTimeOffset.UtcNow) deadline then
                                killProcessGroup child
                                return Error(RunnerError.TimeoutExceeded budgetSpan)
                            elif ct.IsCancellationRequested then
                                killProcessGroup child
                                return Error(RunnerError.ProcessCancelled "Cancelled by token")
                            else
                                let totalBytes = int64 stdoutBytes.Length + int64 stderrBytes.Length
                                let stdoutStr = Encoding.UTF8.GetString(stdoutBytes, 0, stdoutBytes.Length)
                                let stderrStr = Encoding.UTF8.GetString(stderrBytes, 0, stderrBytes.Length)

                                if totalBytes > int64 Spool.ChunkSizeBytes then
                                    let combined = Array.append stdoutBytes stderrBytes
                                    let (tempFile, chunks) = Spool.spoolBytesToTempFile combined

                                    return
                                        Ok(RunnerOutcome.Spooled(exitCode, tempFile, totalBytes, chunks.Length, chunks))
                                else
                                    return Ok(RunnerOutcome.Completed(exitCode, stdoutStr, stderrStr, false))
                with ex ->
                    return Error(RunnerError.ExecutionFailed ex.Message)
            finally
                if isLarge then
                    releaseLargeGate ()
        }
