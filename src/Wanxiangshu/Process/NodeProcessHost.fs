namespace Wanxiangshu.Process

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.AsyncSupport

/// JS-host process adapter. Non-PTY child-process execution delegates to
/// wanxiangli/supervision (SupervisedProcess); spool-file I/O stays here so the rest
/// of the Process namespace remains free of raw Node interop.
module NodeProcessHost =

    [<Import("SupervisedProcess", "wanxiangli/supervision")>]
    let private SupervisedProcessCtor: obj = jsNative

    [<Import("constants", "node:os")>]
    let private osConstants: obj = jsNative

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

    [<Import("statSync", "node:fs")>]
    let private statSync (path: string) : obj = jsNative

    [<Import("accessSync", "node:fs")>]
    let private accessSync (path: string) (mode: int) : unit = jsNative

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

    [<Emit("""
        ((envEntries) => {
            const out = Object.create(null);
            if (process && process.env) {
                for (const k of Object.keys(process.env)) {
                    const v = process.env[k];
                    if (typeof k === 'string' && k.length > 0 && !k.includes('=') && !k.includes('\0') && typeof v === 'string' && !v.includes('\0')) {
                        out[k] = v;
                    }
                }
            }
            if (Array.isArray(envEntries)) {
                for (const pair of envEntries) {
                    const k = pair[0];
                    const v = pair[1];
                    out[k] = v;
                }
            }
            return out;
        })($0)
    """)>]
    let private buildEnvSnapshot (entries: (string * string) array) : obj = jsNative

    [<Emit("""
        ((rawCwd, pathJoin) => {
            const base = (typeof rawCwd === 'string' && rawCwd.length > 0) ? rawCwd : process.cwd();
            return base.startsWith('/') ? base : pathJoin(process.cwd(), base);
        })($0, $1)
    """)>]
    let private resolveAbsoluteCwd (rawCwd: obj) (joinFn: string -> string -> string) : string = jsNative

    [<Emit("""
        ((fileName, cwd, envObj, pathJoin, statSync, accessSync) => {
            if (typeof fileName !== 'string' || fileName.length === 0 || fileName.includes('\0')) {
                return null;
            }
            const isExecutableFile = (candidate) => {
                try {
                    const st = statSync(candidate);
                    if (!st || typeof st.isFile !== 'function' || !st.isFile()) return false;
                    accessSync(candidate, 1);
                    return true;
                } catch (_) {
                    return false;
                }
            };
            if (fileName.startsWith('/')) {
                return fileName;
            }
            if (fileName.includes('/')) {
                return pathJoin(cwd, fileName);
            }
            const pathText = (envObj && typeof envObj.PATH === 'string')
                ? envObj.PATH
                : ((process && process.env && typeof process.env.PATH === 'string') ? process.env.PATH : '');
            for (const dir of pathText.split(':')) {
                const folder = dir.startsWith('/') ? dir : pathJoin(cwd, dir);
                const candidate = pathJoin(folder, fileName);
                if (isExecutableFile(candidate)) {
                    return candidate;
                }
            }
            return pathJoin('/nonexistent-wanxiang-bin', fileName);
        })($0, $1, $2, $3, $4, $5)
    """)>]
    let private resolveExecutablePath
        (fileName: string)
        (cwd: string)
        (envObj: obj)
        (joinFn: string -> string -> string)
        (statFn: string -> obj)
        (accessFn: string -> int -> unit)
        : string =
        jsNative

    [<Emit("""
        (async function(SupervisedProcess, executable, args, cwd, env, stdinText, onStdout, onStderr, onClose, onFailure, constants) {
            let session;
            let killRequested = false;
            try {
                session = new SupervisedProcess({
                    executable,
                    args: Array.from(args),
                    cwd,
                    env,
                    tailBytes: 0,
                    maxWriteBytes: 16777216,
                });
            } catch (err) {
                return { ok: false, error: (err && err.message) ? String(err.message) : 'Failed to create SupervisedProcess' };
            }
            const startResult = await session.started();
            if (!startResult || startResult.kind === 'Failed') {
                const msg = (startResult && startResult.error && startResult.error.message)
                    ? String(startResult.error.message)
                    : ('Failed to spawn process: ' + executable);
                return { ok: false, error: msg };
            }
            const requestKill = () => {
                try {
                    const result = session.signal('SIGKILL');
                    if (result.kind === 'Requested') killRequested = true;
                    if (result.kind === 'Failed') onFailure(String(result.error.message));
                } catch (error) {
                    onFailure(error && error.message ? String(error.message) : String(error));
                }
            };
            const reportClose = state => {
                const expectedUnknownExit = killRequested && state && state.failure
                    && state.failure.code === 'UNKNOWN_TARGET_EXIT';
                if (state && state.failure && !expectedUnknownExit) {
                    onFailure(String(state.failure.message || 'Process supervision failed'));
                }
                const exitInfo = state && state.exit;
                const signalNumber = exitInfo && exitInfo.signal && constants.signals[exitInfo.signal];
                const code = (exitInfo && typeof exitInfo.code === 'number')
                    ? exitInfo.code
                    : (typeof signalNumber === 'number' ? 128 + signalNumber : -1);
                onClose(code);
            };
            (async () => {
                try {
                    if (typeof stdinText === 'string' && stdinText.length > 0) {
                        const encoded = new TextEncoder().encode(stdinText);
                        for (let pos = 0; pos < encoded.byteLength; pos += 16777216) {
                            const part = encoded.slice(pos, Math.min(pos + 16777216, encoded.byteLength));
                            const w = await session.write(part);
                            if (!w || w.kind !== 'Written') break;
                        }
                    }
                    session.endInput();
                } catch (_) {
                    try { session.endInput(); } catch (_) {}
                }
            })();
            (async () => {
                try {
                    while (true) {
                        const output = await session.takeOutput();
                        if (output.kind === 'Chunk') {
                            const handler = output.channel === 'stdout' ? onStdout : onStderr;
                            handler(output.bytes);
                        } else if (output.kind === 'Closed') {
                            reportClose(output.state);
                            break;
                        } else {
                            throw new Error(output.error ? output.error.message : 'Process output was discarded');
                        }
                    }
                } catch (error) {
                    onFailure(error && error.message ? String(error.message) : String(error));
                    requestKill();
                    session.discardOutput();
                    while (true) {
                        const waited = await session.wait(1000);
                        if (waited.kind === 'Closed') {
                            reportClose(waited.state);
                            break;
                        }
                    }
                }
            })();
            return { ok: true, session, kill: requestKill };
        })($0, $1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
    """)>]
    let private startProcessSession
        (ctor: obj)
        (executable: string)
        (args: string array)
        (cwd: string)
        (envObj: obj)
        (stdinText: obj)
        (onStdout: byte[] -> unit)
        (onStderr: byte[] -> unit)
        (onClose: int -> unit)
        (onFailure: string -> unit)
        (constants: obj)
        : Task<obj> =
        jsNative

    /// Handle returned by spawn.
    ///
    /// `Exit` carries the target code, or 128 plus its native signal number.
    /// `Exited` is set ONLY by the close/error handlers. `Kill` must not set it:
    /// sending SIGKILL is not the process ending, and conflating the two makes
    /// "kill sent" look like "exit observed" to every waiter.
    /// DSL-state-combination: physical — child process exit receipt and callback buffer.
    type ChildProcess =
        { Process: obj
          Exit: TaskCompletionSource<int>
          Kill: unit -> unit
          Exited: bool ref
          OnExited: (unit -> unit) ResizeArray
          Failure: string option ref }

    let private notifyExitedList (callbacks: (unit -> unit) ResizeArray) =
        let cbs = callbacks |> Seq.toList
        callbacks.Clear()

        for cb in cbs do
            cb ()

    let notifyExited (child: ChildProcess) = notifyExitedList child.OnExited

    let private buildEnv (envOpt: Map<string, string> option) : obj =
        let entries =
            match envOpt with
            | Some envMap -> envMap |> Map.toArray
            | None -> [||]

        buildEnvSnapshot entries

    let private cwdValue (cmd: Command) (ctx: ProcessContext) : string =
        let raw =
            match cmd.WorkingDirectory |> Option.orElse ctx.WorkingDirectory with
            | Some wd -> box wd
            | None -> null

        resolveAbsoluteCwd raw pathJoin

    let private stdinValue (stdinOpt: string option) : obj =
        match stdinOpt with
        | Some text -> box text
        | None -> null

    let private invokeKill (killObj: obj) =
        try
            emitJsExpr killObj "$0()" |> ignore
        with _ ->
            ()

    let private killIfStillRunning (exitedRef: bool ref) (killObj: obj) =
        if not exitedRef.Value then
            invokeKill killObj

    let private markProcessClosed
        (exitedRef: bool ref)
        (exitTcs: TaskCompletionSource<int>)
        (onExited: (unit -> unit) ResizeArray)
        (code: int)
        =
        if not exitedRef.Value then
            exitedRef.Value <- true
            trySetResult exitTcs code |> ignore
            notifyExitedList onExited

    let private finalizeStartedSession
        (started: obj)
        (exitTcs: TaskCompletionSource<int>)
        (exitedRef: bool ref)
        (onExited: (unit -> unit) ResizeArray)
        (failure: string option ref)
        (ct: CancellationToken)
        : Result<ChildProcess, string> =
        let ok = emitJsExpr started "Boolean($0 && $0.ok)"

        if not ok then
            let errText: string =
                emitJsExpr started "String(($0 && $0.error) || 'Failed to spawn process')"

            Error errText
        else
            let sessionObj: obj = emitJsExpr started "$0.session"
            let killObj: obj = emitJsExpr started "$0.kill"
            use _ = ct.Register(fun () -> killIfStillRunning exitedRef killObj)

            Ok
                { Process = sessionObj
                  Exit = exitTcs
                  Kill = (fun () -> invokeKill killObj)
                  Exited = exitedRef
                  OnExited = onExited
                  Failure = failure }

    let private launchExecutableSession
        (executable: string)
        (cwd: string)
        (envObj: obj)
        (cmd: Command)
        (onStdout: byte[] -> unit)
        (onStderr: byte[] -> unit)
        (ct: CancellationToken)
        : Task<Result<ChildProcess, string>> =
        task {
            let exitTcs = TaskCompletionSource<int>()
            // DSL-MUTABLE: algorithm-scratch — exit callback list accumulator
            let onExited = ResizeArray<unit -> unit>()
            // DSL-MUTABLE: cancellation — child process exit flag.
            let exitedRef = ref false
            let onClose = markProcessClosed exitedRef exitTcs onExited
            let failure = ref None

            let! started =
                startProcessSession
                    SupervisedProcessCtor
                    executable
                    (cmd.Arguments |> List.toArray)
                    cwd
                    envObj
                    (stdinValue cmd.Stdin)
                    onStdout
                    onStderr
                    onClose
                    (fun message ->
                        if failure.Value.IsNone then
                            failure.Value <- Some message)
                    osConstants

            return finalizeStartedSession started exitTcs exitedRef onExited failure ct
        }

    let private spawnOrReject
        (cmd: Command)
        (ctx: ProcessContext)
        (onStdout: byte[] -> unit)
        (onStderr: byte[] -> unit)
        (ct: CancellationToken)
        =
        task {
            let envObj = buildEnv cmd.Environment
            let cwd = cwdValue cmd ctx

            let executable =
                resolveExecutablePath cmd.FileName cwd envObj pathJoin statSync accessSync

            if isNull executable then
                return Error(sprintf "Failed to spawn process: %s" cmd.FileName)
            else
                return! launchExecutableSession executable cwd envObj cmd onStdout onStderr ct
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
        // DSL-MUTABLE: algorithm-scratch — read loop file offset
        let mutable position = 0
        // DSL-MUTABLE: algorithm-scratch — read loop done flag
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
