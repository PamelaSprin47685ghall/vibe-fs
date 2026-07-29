namespace Wanxiangshu.Next.Process

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.AsyncSupport

/// JS-host process adapter. All Node/Bun interop for child-process spawn and
/// spool-file I/O lives here so the rest of the Process namespace stays pure.
module NodeProcessHost =

    [<Import("spawn", "node:child_process")>]
    let private spawnChild (command: string) (args: string array) (options: obj) : obj = jsNative

    [<Emit("Object.assign({}, process.env)")>]
    let private processEnv () : obj = jsNative

    [<Emit("((child, text) => { try { if (child && child.stdin) { child.stdin.write(text, 'utf8'); child.stdin.end(); } } catch (_) {} })($0, $1)")>]
    let private writeStdinText (child: obj) (text: string) : unit = jsNative

    [<Emit("((child) => { try { if (child && child.stdin) child.stdin.end(); } catch (_) {} })($0)")>]
    let private closeStdin (child: obj) : unit = jsNative

    [<Emit("(() => { try { if ($0 && $0.pid) { process.kill(-$0.pid, 'SIGKILL'); } } catch (_) { try { if ($0 && typeof $0.kill === 'function') $0.kill('SIGKILL'); } catch (_) {} } })()")>]
    let private killProcessGroup (child: obj) : unit = jsNative

    [<Emit("($0 && $0.stdout) || null")>]
    let private stdoutOf (child: obj) : obj = jsNative

    [<Emit("($0 && $0.stderr) || null")>]
    let private stderrOf (child: obj) : obj = jsNative

    [<Emit("new Uint8Array($0.buffer, $0.byteOffset, $0.byteLength)")>]
    let private toBytes (chunk: obj) : byte[] = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string) (b: string) : string = jsNative

    [<Import("tmpdir", "node:os")>]
    let private tmpdir () : string = jsNative

    [<Import("writeFileSync", "node:fs")>]
    let private writeFileSync (path: string) (data: byte[]) : unit = jsNative

    [<Import("appendFileSync", "node:fs")>]
    let private appendFileSync (path: string) (data: byte[]) : unit = jsNative

    [<Import("openSync", "node:fs")>]
    let private openSync (path: string) (flags: string) : int = jsNative

    [<Import("readSync", "node:fs")>]
    let private readSync (fd: int) (buffer: byte[]) (offset: int) (length: int) (position: int) : int = jsNative

    [<Import("closeSync", "node:fs")>]
    let private closeSync (fd: int) : unit = jsNative

    [<Import("createReadStream", "node:fs")>]
    let private createReadStream (path: string) (options: obj) : obj = jsNative

    [<Import("unlinkSync", "node:fs")>]
    let private unlinkSync (path: string) : unit = jsNative

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    [<Emit("""
        (async function(stream, consume) {
            for await (const input of stream) {
                const bytes = Buffer.from(input);
                for (let offset = 0; offset < bytes.length; offset += 204800) {
                    const end = Math.min(offset + 204800, bytes.length);
                    await consume(new Uint8Array(bytes.buffer, bytes.byteOffset + offset, end - offset));
                }
            }
        })($0, $1)
    """)>]
    let private consumeStreamAsync (stream: obj) (consume: byte[] -> Task<unit>) : Task<unit> = jsNative

    /// Handle returned by spawn.
    ///
    /// `Exit` carries the real exit code and nothing else. It used to be
    /// `int * bool`, where the bool meant "timed out" — but a process does not know
    /// whether someone was waiting on a deadline, so that flag was the waiter's
    /// knowledge stored on the process. It let the timeout path complete the cell
    /// itself with a fabricated `(-1, true)` instead of waiting for the real exit,
    /// which EXEC-011 requires.
    ///
    /// `Exited` is set ONLY by the close/error handlers. `Kill` must not set it:
    /// sending SIGKILL is not the process ending, and conflating the two makes
    /// "kill sent" look like "exit observed" to every waiter.
    type ChildProcess =
        { Process: obj
          Exit: TaskCompletionSource<int>
          Kill: unit -> unit
          Exited: bool ref
          OnExited: (unit -> unit) ResizeArray }

    let private notifyExitedList (callbacks: (unit -> unit) ResizeArray) =
        let cbs = callbacks |> Seq.toList
        callbacks.Clear()

        for cb in cbs do
            cb ()

    let notifyExited (child: ChildProcess) = notifyExitedList child.OnExited

    let private buildEnv (envOpt: Map<string, string> option) : obj =
        let jsEnv = processEnv ()

        match envOpt with
        | Some envMap ->
            for KeyValue(k, v) in envMap do
                emitJsExpr (jsEnv, k, v) "$0[$1] = $2" |> ignore
        | None -> ()

        jsEnv

    let private cwdValue (cmd: Command) (ctx: ProcessContext) : obj =
        match cmd.WorkingDirectory with
        | Some wd -> box wd
        | None ->
            match ctx.WorkingDirectory with
            | Some wd -> box wd
            | None -> emitJsExpr () "process.cwd()"

    let private recordChunk (handler: byte[] -> unit) (chunk: obj) =
        try
            if not (isNull chunk) then
                let bytes = toBytes chunk

                if bytes.Length > 0 then
                    handler bytes
        with _ ->
            ()

    let spawn
        (cmd: Command)
        (ctx: ProcessContext)
        (onStdout: byte[] -> unit)
        (onStderr: byte[] -> unit)
        (ct: CancellationToken)
        : Task<Result<ChildProcess, string>> =
        task {
            if ct.IsCancellationRequested then
                return Error "Cancelled before spawn"
            else
                let options =
                    createObj
                        [ "env" ==> buildEnv cmd.Environment
                          "cwd" ==> cwdValue cmd ctx
                          "detached" ==> true
                          "stdio" ==> [| "pipe"; "pipe"; "pipe" |] ]

                let child =
                    try
                        spawnChild cmd.FileName (cmd.Arguments |> List.toArray) options
                    with _ ->
                        null

                if isNull child then
                    return Error(sprintf "Failed to spawn process: %s" cmd.FileName)
                else
                    let exitTcs = TaskCompletionSource<int>()
                    let onExited = ResizeArray<unit -> unit>()

                    let stdout = stdoutOf child
                    let stderr = stderrOf child

                    if not (isNull stdout) then
                        emitJsExpr (stdout, recordChunk onStdout) "$0.on('data', $1)" |> ignore

                    if not (isNull stderr) then
                        emitJsExpr (stderr, recordChunk onStderr) "$0.on('data', $1)" |> ignore

                    let exitedRef = ref false

                    emitJsExpr
                        (child,
                         (fun (code: obj) ->
                             let c = if isNull code then 0 else unbox<int> code
                             exitedRef.Value <- true
                             trySetResult exitTcs c |> ignore
                             notifyExitedList onExited),
                         (fun _err ->
                             exitedRef.Value <- true
                             trySetResult exitTcs -1 |> ignore
                             notifyExitedList onExited))
                        "if ($0) { $0.on('close', function(code) { $1(typeof code === 'number' ? code : 0); }); $0.on('error', function(err) { $2(err); }); }"
                    |> ignore

                    match cmd.Stdin with
                    | Some text -> writeStdinText child text
                    | None -> closeStdin child

                    use _ =
                        ct.Register(fun () ->
                            if not exitedRef.Value then
                                killProcessGroup child)

                    return
                        Ok
                            { Process = child
                              Exit = exitTcs
                              // EXEC-011: SIGKILL the whole process group, then let
                              // the close handler report the real exit. Marking the
                              // child exited here would let a waiter return before
                              // the process actually died.
                              Kill =
                                fun () ->
                                    try
                                        killProcessGroup child
                                    with _ ->
                                        ()
                              Exited = exitedRef
                              OnExited = onExited }
        }

    let tempPath () : string =
        pathJoin (tmpdir ()) (sprintf "spool-%s.tmp" (Guid.NewGuid().ToString("N").Substring(0, 8)))

    let writeFile (path: string) (data: byte[]) : unit =
        if not (isNull data) then
            writeFileSync path data

    let appendFile (path: string) (data: byte[]) : unit =
        if not (isNull data) && data.Length > 0 then
            appendFileSync path data

    let deleteFile (path: string) : unit =
        try
            if not (String.IsNullOrWhiteSpace path) && existsSync path then
                unlinkSync path
        with _ ->
            ()

    let readFileSyncChunks (path: string) (chunkSize: int) (consume: byte[] -> unit) : unit =
        let fd = openSync path "r"
        let buffer = Array.zeroCreate<byte> chunkSize
        let mutable position = 0
        let mutable done' = false

        try
            while not done' do
                let count = readSync fd buffer 0 chunkSize position

                if count <= 0 then
                    done' <- true
                else
                    let chunk = Array.zeroCreate<byte> count
                    Array.blit buffer 0 chunk 0 count
                    consume chunk
                    position <- position + count
        finally
            closeSync fd

    let readFileAsyncChunks (path: string) (chunkSize: int) (consume: byte[] -> Task<unit>) : Task<unit> =
        let options = createObj [ "highWaterMark" ==> chunkSize ]
        createReadStream path options |> fun stream -> consumeStreamAsync stream consume
