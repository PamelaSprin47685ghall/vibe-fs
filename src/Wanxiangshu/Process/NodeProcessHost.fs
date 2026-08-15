namespace Wanxiangshu.Process

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.AsyncSupport

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
    /// DSL-state-combination: physical — child process exit receipt and callback buffer.
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

    let private assignEnvMap (jsEnv: obj) (envMap: Map<string, string>) =
        for KeyValue(k, v) in envMap do
            emitJsExpr (jsEnv, k, v) "$0[$1] = $2" |> ignore

    let private buildEnv (envOpt: Map<string, string> option) : obj =
        let jsEnv = processEnv ()

        match envOpt with
        | Some envMap -> assignEnvMap jsEnv envMap
        | None -> ()

        jsEnv

    let private cwdValue (cmd: Command) (ctx: ProcessContext) : obj =
        match cmd.WorkingDirectory |> Option.orElse ctx.WorkingDirectory with
        | Some wd -> box wd
        | None -> emitJsExpr () "process.cwd()"

    let private tryChunkBytes (chunk: obj) : byte[] option =
        let bytes = if isNull chunk then [||] else toBytes chunk

        if bytes.Length = 0 then
            None
        else
            Some bytes

    let private recordChunk (handler: byte[] -> unit) (chunk: obj) =
        try
            tryChunkBytes chunk |> Option.iter handler
        with _ ->
            ()

    let private trySpawnChild (cmd: Command) (options: obj) =
        try
            spawnChild cmd.FileName (cmd.Arguments |> List.toArray) options
        with _ ->
            null

    let private attachStream (stream: obj) (handler: byte[] -> unit) =
        if not (isNull stream) then
            emitJsExpr (stream, recordChunk handler) "$0.on('data', $1)" |> ignore

    let private applyStdin (child: obj) (stdin: string option) =
        match stdin with
        | Some text -> writeStdinText child text
        | None -> closeStdin child

    let private killIfStillRunning (exitedRef: bool ref) (child: obj) =
        if not exitedRef.Value then
            killProcessGroup child

    let private makeKill (child: obj) =
        fun () ->
            try
                killProcessGroup child
            with _ ->
                ()

    let private wireExitHandlers (child: obj) (exitTcs: TaskCompletionSource<int>) (exitedRef: bool ref) (onExited: (unit -> unit) ResizeArray) =
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

    let private assembleChild (child: obj) (onStdout: byte[] -> unit) (onStderr: byte[] -> unit) (cmd: Command) (ct: CancellationToken) =
        let exitTcs = TaskCompletionSource<int>()
        let onExited = ResizeArray<unit -> unit>()
        let exitedRef = ref false

        attachStream (stdoutOf child) onStdout
        attachStream (stderrOf child) onStderr
        wireExitHandlers child exitTcs exitedRef onExited
        applyStdin child cmd.Stdin

        use _ = ct.Register(fun () -> killIfStillRunning exitedRef child)

        { Process = child
          Exit = exitTcs
          // EXEC-011: SIGKILL the whole process group, then let
          // the close handler report the real exit. Marking the
          // child exited here would let a waiter return before
          // the process actually died.
          Kill = makeKill child
          Exited = exitedRef
          OnExited = onExited }

    let private spawnOrReject
        (cmd: Command)
        (ctx: ProcessContext)
        (onStdout: byte[] -> unit)
        (onStderr: byte[] -> unit)
        (ct: CancellationToken)
        =
        task {
            let options =
                createObj
                    [ "env" ==> buildEnv cmd.Environment
                      "cwd" ==> cwdValue cmd ctx
                      "detached" ==> true
                      "stdio" ==> [| "pipe"; "pipe"; "pipe" |] ]

            let child = trySpawnChild cmd options

            if isNull child then
                return Error(sprintf "Failed to spawn process: %s" cmd.FileName)
            else
                return Ok(assembleChild child onStdout onStderr cmd ct)
        }

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
                return! spawnOrReject cmd ctx onStdout onStderr ct
        }

    let tempPath () : string =
        pathJoin (tmpdir ()) (sprintf "spool-%s.tmp" (Guid.NewGuid().ToString("N").Substring(0, 8)))

    let writeFile (path: string) (data: byte[]) : unit =
        if not (isNull data) then
            writeFileSync path data

    let appendFile (path: string) (data: byte[]) : unit =
        if not (isNull data) && data.Length > 0 then
            appendFileSync path data

    let private unlinkIfPresent (path: string) =
        if not (String.IsNullOrWhiteSpace path) && existsSync path then
            unlinkSync path

    let deleteFile (path: string) : unit =
        try
            unlinkIfPresent path
        with _ ->
            ()

    let private applyReadStep (fd: int) (buffer: byte[]) (chunkSize: int) (position: int) (consume: byte[] -> unit) =
        let count = readSync fd buffer 0 chunkSize position

        if count <= 0 then
            struct (position, true)
        else
            let chunk = Array.zeroCreate<byte> count
            Array.blit buffer 0 chunk 0 count
            consume chunk
            struct (position + count, false)

    let private drainAllChunks (fd: int) (chunkSize: int) (consume: byte[] -> unit) =
        let buffer = Array.zeroCreate<byte> chunkSize
        // DSL-MUTABLE: buffer — read loop file offset
        let mutable position = 0
        // DSL-MUTABLE: buffer — read loop done flag
        let mutable done' = false

        while not done' do
            let struct (next, finished) = applyReadStep fd buffer chunkSize position consume
            position <- next
            done' <- finished

    let readFileSyncChunks (path: string) (chunkSize: int) (consume: byte[] -> unit) : unit =
        let fd = openSync path "r"

        try
            drainAllChunks fd chunkSize consume
        finally
            closeSync fd

    let readFileAsyncChunks (path: string) (chunkSize: int) (consume: byte[] -> Task<unit>) : Task<unit> =
        let options = createObj [ "highWaterMark" ==> chunkSize ]
        createReadStream path options |> fun stream -> consumeStreamAsync stream consume
