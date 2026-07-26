namespace Wanxiangshu.Next.Process

open System
open System.Text
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

[<RequireQualifiedAccess>]
type RunnerOutcome =
    | Completed of exitCode: int * stdout: string * stderr: string * spooled: bool
    | Spooled of exitCode: int * spoolPath: string * totalBytes: int64 * chunkCount: int
    | OutputExceeded of bytesWritten: int64 * spoolPath: string option

[<RequireQualifiedAccess>]
type RunnerError =
    | TimeoutExceeded of duration: TimeSpan
    | SpawnFailed of reason: string
    | ProcessCancelled of reason: string
    | ExecutionFailed of reason: string

module RunnerCore =

    [<Import("spawn", "node:child_process")>]
    let private spawnChildProcess (command: string) (args: string array) (options: obj) : obj = jsNative

    [<Emit("((child, text) => { try { if (child && child.stdin) { child.stdin.write(text, 'utf8'); child.stdin.end(); } } catch (_) {} return undefined; })($0, $1)")>]
    let private writeStdin (child: obj) (text: string) : unit = jsNative

    [<Emit("((child) => { try { if (child && child.stdin) child.stdin.end(); } catch (_) {} return undefined; })($0)")>]
    let private closeStdin (child: obj) : unit = jsNative

    let private outputThreshold (estimate: ProcessEstimate) : int64 =
        let (OutputBytes bytes) = estimate.EstimatedOutput

        if bytes <= 0L then 0L
        elif bytes > Int64.MaxValue / 3L then Int64.MaxValue
        else bytes * 3L

    let private executeChild
        (cmd: Command)
        (estimate: ProcessEstimate)
        (ctx: ProcessContext)
        (budgetSpan: TimeSpan)
        (deadline: Deadline)
        (ct: CancellationToken)
        : Task<Result<RunnerOutcome, RunnerError>> =
        task {
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

            let child =
                try
                    spawnChildProcess cmd.FileName (cmd.Arguments |> List.toArray) jsOptions
                with _ ->
                    null

            if isNull child then
                return Error(RunnerError.SpawnFailed("Failed to spawn process: " + cmd.FileName))
            else
                let stdoutChunks = ResizeArray<byte[]>()
                let stderrChunks = ResizeArray<byte[]>()
                let combinedChunks = ResizeArray<byte[]>()
                let mutable bytesObserved = 0L
                let mutable spool: Spool.StreamingSpool option = None
                let outputLimit = outputThreshold estimate

                let toBytes (chunk: obj) : byte[] =
                    emitJsExpr chunk "new Uint8Array($0.buffer, $0.byteOffset, $0.byteLength)"
                    |> unbox<byte[]>

                let recordChunk (target: ResizeArray<byte[]>) (chunk: obj) =
                    let bytes = toBytes chunk

                    if bytes.Length > 0 then
                        bytesObserved <- bytesObserved + int64 bytes.Length

                        match spool with
                        | Some active -> Spool.appendStreamingSpool active bytes
                        | None ->
                            target.Add bytes

                            if bytesObserved > outputLimit then
                                let active = Spool.startStreamingSpool ()

                                for previous in combinedChunks do
                                    Spool.appendStreamingSpool active previous

                                Spool.appendStreamingSpool active bytes
                                combinedChunks.Clear()
                                stdoutChunks.Clear()
                                stderrChunks.Clear()
                                spool <- Some active
                            else
                                combinedChunks.Add bytes

                emitJsExpr
                    (child, (fun chunk -> recordChunk stdoutChunks chunk))
                    "if ($0 && $0.stdout) $0.stdout.on('data', $1);"
                |> ignore

                emitJsExpr
                    (child, (fun chunk -> recordChunk stderrChunks chunk))
                    "if ($0 && $0.stderr) $0.stderr.on('data', $1);"
                |> ignore

                match cmd.Stdin with
                | Some stdinText -> writeStdin child stdinText
                | None -> closeStdin child

                let completion = TaskCompletionSource<int * bool>()
                let mutable finished = false
                let mutable timerId: obj = null
                let mutable timedOut = false
                let mutable cancelled = false
                let mutable childClosed = false
                let mutable stdoutEnded = isNull (emitJsExpr child "$0 && $0.stdout")
                let mutable stderrEnded = isNull (emitJsExpr child "$0 && $0.stderr")
                let mutable exitCode = 0

                let clock = fun () -> DateTimeOffset.UtcNow

                let tryFinish () =
                    if childClosed && stdoutEnded && stderrEnded && not finished then
                        finished <- true

                        if not (isNull timerId) then
                            emitJsExpr timerId "clearTimeout($0)" |> ignore

                        completion.SetResult(exitCode, timedOut)

                // Huge legal estimates (tens of days) overflow int/JS timer max. Wait in
                // capped segments against the absolute deadline so the command runs to
                // completion instead of timing out immediately. ponytail: 0x7FFFFFFF ms cap.
                let armTimer =
                    let rec loop () =
                        let ms = Deadline.nextWaitMs clock deadline

                        if ms <= 0 then
                            if not finished then
                                timedOut <- true
                                RunnerPrimitives.killProcessGroup child
                        else
                            timerId <-
                                emitJsExpr
                                    (ms,
                                     (fun () ->
                                         if finished then
                                             ()
                                         elif Deadline.isExpired clock deadline then
                                             timedOut <- true
                                             RunnerPrimitives.killProcessGroup child
                                         else
                                             loop ()))
                                    "setTimeout($1, $0)"

                    loop

                armTimer ()

                emitJsExpr
                    (child,
                     (fun code ->
                         exitCode <- code
                         childClosed <- true
                         tryFinish ()),
                     (fun _err ->
                         exitCode <- -1
                         childClosed <- true
                         tryFinish ()))
                    "if ($0) { $0.on('close', function(code) { $1(typeof code === 'number' ? code : 0); }); $0.on('error', function(err) { $2(err); }); }"
                |> ignore

                emitJsExpr
                    (child,
                     (fun () ->
                         stdoutEnded <- true
                         tryFinish ()))
                    "if ($0 && $0.stdout) $0.stdout.on('end', $1);"
                |> ignore

                emitJsExpr
                    (child,
                     (fun () ->
                         stderrEnded <- true
                         tryFinish ()))
                    "if ($0 && $0.stderr) $0.stderr.on('end', $1);"
                |> ignore

                use cancellationRegistration =
                    ct.Register(fun () ->
                        cancelled <- true
                        RunnerPrimitives.killProcessGroup child)

                if ct.IsCancellationRequested then
                    cancelled <- true
                    RunnerPrimitives.killProcessGroup child

                let! (completedCode, wasTimedOut) = completion.Task
                exitCode <- completedCode
                timedOut <- timedOut || wasTimedOut

                if timedOut || Deadline.isExpired (fun () -> DateTimeOffset.UtcNow) deadline then
                    return Error(RunnerError.TimeoutExceeded budgetSpan)
                elif cancelled || ct.IsCancellationRequested then
                    return Error(RunnerError.ProcessCancelled "Cancelled by token")
                else
                    match spool with
                    | Some active ->
                        return
                            Ok(
                                RunnerOutcome.Spooled(
                                    exitCode,
                                    active.Path,
                                    active.BytesWritten,
                                    Spool.chunkCount active.BytesWritten
                                )
                            )
                    | None ->
                        let concatBytes (parts: ResizeArray<byte[]>) =
                            if parts.Count = 0 then
                                [||]
                            else
                                parts |> Seq.toArray |> Array.concat

                        let stdoutBytes = concatBytes stdoutChunks
                        let stderrBytes = concatBytes stderrChunks
                        let stdoutText = Encoding.UTF8.GetString(stdoutBytes, 0, stdoutBytes.Length)
                        let stderrText = Encoding.UTF8.GetString(stderrBytes, 0, stderrBytes.Length)
                        return Ok(RunnerOutcome.Completed(exitCode, stdoutText, stderrText, false))
        }

    let execute
        (cmd: Command)
        (estimate: ProcessEstimate)
        (ctx: ProcessContext)
        (ct: CancellationToken)
        : Task<Result<RunnerOutcome, RunnerError>> =
        task {
            let (RuntimeSeconds estSecs) = estimate.EstimatedRuntime
            let budgetSpan = TimeSpan.FromSeconds(3.0 * estSecs)

            let deadline =
                RunnerPrimitives.calculateDeadline DateTimeOffset.UtcNow estimate.EstimatedRuntime

            let isLarge = estimate.EstimatedMemory = EstimatedMemory.Large

            if isLarge then
                do! LargeGate.acquire ct

            try
                try
                    if ct.IsCancellationRequested then
                        return Error(RunnerError.ProcessCancelled "Cancelled before spawn")
                    else
                        return! executeChild cmd estimate ctx budgetSpan deadline ct
                with ex ->
                    return Error(RunnerError.ExecutionFailed ex.Message)
            finally
                if isLarge then
                    LargeGate.release ()
        }
