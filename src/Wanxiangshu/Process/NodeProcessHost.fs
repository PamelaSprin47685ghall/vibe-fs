namespace Wanxiangshu.Process

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

module ProcessResultWire =
    let private field (source: obj) name : obj =
        if isNull source then null else source?(name)

    let private text source name = string (field source name)

    let private optionalText source name =
        let value = field source name
        if isNull value then None else Some(string value)

    let private readNoExecution reason =
        match text reason "kind" with
        | "InvalidRequest" -> NoExecution.InvalidRequest(text reason "reason")
        | "CancelledBeforeStart" -> NoExecution.CancelledBeforeStart
        | "StartRejected" -> NoExecution.StartRejected(text reason "reason")
        | _ -> failwith "Invalid no-execution receipt"

    let private readExitCode ending =
        let code = field ending "exitCode"

        if emitJsExpr code "Number.isInteger($0) && $0 >= 0 && $0 <= 2147483647" then
            unbox<int> code
        else
            failwith "Invalid exit code"

    let private readEnd ending =
        match text ending "kind" with
        | "Exited" -> EndEvidence.Confirmed(ProgramEnd.Exited(readExitCode ending))
        | "Signaled" -> EndEvidence.Confirmed(ProgramEnd.Signaled(text ending "signal"))
        | "Unconfirmed" -> EndEvidence.Unconfirmed
        | _ -> failwith "Invalid target end evidence"

    let private readStopRequest stop =
        match text stop "kind" with
        | "CancellationRequested" -> StopRequest.CancellationRequested
        | "DeadlineStopRequested" ->
            StopRequest.DeadlineStopRequested(TimeSpan.FromMilliseconds(unbox<float> (field stop "durationMs")))
        | _ -> failwith "Invalid stop request"

    let private readStop stop =
        if isNull stop then None else Some(readStopRequest stop)

    let private readCapturedOutput output =
        match text output "kind" with
        | "InMemory" -> CapturedOutput.InMemory(text output "stdout", text output "stderr")
        | _ -> failwith "Expected in-memory text output"

    let private readOutput output =
        if isNull output then
            None
        else
            Some(readCapturedOutput output)

    let private readReceipt raw =
        match text raw "kind" with
        | "NotStarted" -> ProcessResult.NotStarted(readNoExecution (field raw "reason"))
        | "Observed" ->
            ProcessResult.Observed
                { End = readEnd (field raw "ending")
                  StopRequested = readStop (field raw "stopRequested")
                  Output = readOutput (field raw "output")
                  Fault = optionalText raw "fault" }
        | _ -> failwith "Invalid process receipt"

    let read (raw: obj) =
        try
            readReceipt raw
        with error ->
            ProcessResult.Observed
                { End = EndEvidence.Unconfirmed
                  StopRequested = None
                  Output = None
                  Fault = Some error.Message }

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
        (function(SupervisedProcess, executable, args, cwd, env, stdinBytes, onStdout, onStderr, onClose, onFailure, onRejected, keepInputOpen) {
            let session;
            let killRequested = false;
            let targetReady = false;
            const pendingSignals = [];
            const input = stdinBytes == null ? null : Uint8Array.from(stdinBytes);
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
            const requestSignal = name => {
                if (!['SIGTERM', 'SIGKILL', 'SIGINT', 'SIGHUP'].includes(name)) {
                    return { kind: 'Failed', error: { message: 'Unsupported signal' } };
                }
                if (name === 'SIGKILL') {
                    killRequested = true;
                    return session.signal(name);
                }
                if (!targetReady) {
                    pendingSignals.push(name);
                    return { kind: 'Requested' };
                }
                return session.signal(name);
            };
            const requestKill = () => {
                killRequested = true;
                try {
                    const result = requestSignal('SIGKILL');
                    if (result.kind === 'Failed') onFailure(String(result.error.message));
                } catch (error) {
                    onFailure(error && error.message ? String(error.message) : String(error));
                }
            };
            const reportClose = state => {
                if (state && state.failure) {
                    onFailure(String(state.failure.message || 'Process supervision failed'));
                }
                onClose();
            };
            const awaitClose = async () => {
                while (true) {
                    const waited = await session.wait(1000);
                    if (waited.kind === 'Closed') {
                        reportClose(waited.state);
                        return;
                    }
                }
            };
            (async () => {
                try {
                    const started = await session.started();
                    if (!started || started.kind !== 'Started') {
                        const error = started && started.error;
                        const message = String(error && error.message || 'Target start was not confirmed');
                        if (error && error.operation === 'spawn') onRejected(message);
                        else onFailure(message);
                        await awaitClose();
                        return;
                    }
                    targetReady = true;
                    if (killRequested) {
                        requestKill();
                        pendingSignals.length = 0;
                    } else {
                        for (const name of pendingSignals.splice(0)) {
                            const requested = requestSignal(name);
                            if (requested.kind === 'Failed') onFailure(String(requested.error.message));
                        }
                    }
                    if (input !== null) {
                        for (let pos = 0; pos < input.byteLength; pos += 16777216) {
                            if (killRequested) break;
                            const part = input.slice(pos, Math.min(pos + 16777216, input.byteLength));
                            const w = await session.write(part);
                            if (!w || w.kind !== 'Written') {
                                onFailure(String(w && w.error && w.error.message || 'Process input was not delivered'));
                                requestKill();
                                break;
                            }
                        }
                    }
                    if (!keepInputOpen) session.endInput();
                } catch (error) {
                    onFailure(String(error && error.message || error));
                    requestKill();
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
                    await awaitClose();
                }
            })();
            return { ok: true, session, kill: requestKill, signal: requestSignal };
        })($0, $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)
    """)>]
    let private startProcessSession
        (ctor: obj)
        (executable: string)
        (args: string array)
        (cwd: string)
        (envObj: obj)
        (stdinBytes: obj)
        (onStdout: byte[] -> unit)
        (onStderr: byte[] -> unit)
        (onClose: unit -> unit)
        (onFailure: string -> unit)
        (onRejected: string -> unit)
        (keepInputOpen: bool)
        : obj =
        jsNative

    /// Exited means capture and scope closed. Observe reads the target separately.
    type ChildProcess =
        { Process: obj
          Observe: unit -> EndEvidence
          Kill: unit -> unit
          RequestSignal: string -> Result<unit, string>
          Exited: bool ref
          OnExited: (unit -> unit) ResizeArray
          Failure: string option ref
          Rejection: NoExecution option ref }

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

    let private stdinValue (stdinOpt: byte[] option) : obj =
        match stdinOpt with
        | Some bytes -> box bytes
        | None -> null

    let private invokeKill (killObj: obj) =
        try
            emitJsExpr killObj "$0()" |> ignore
        with _ ->
            ()

    let private markProcessClosed (exitedRef: bool ref) (onExited: (unit -> unit) ResizeArray) () =
        if not exitedRef.Value then
            exitedRef.Value <- true
            notifyExitedList onExited

    let private rememberFailure (failure: string option ref) message =
        if failure.Value.IsNone then
            failure.Value <- Some message

    let private unknownEnd native failure =
        if not (isNull native) then
            rememberFailure failure "Invalid target end receipt"

        EndEvidence.Unconfirmed

    let private readNativeEnd native failure =
        let validCode: bool =
            emitJsExpr
                native
                "Boolean($0 && Number.isInteger($0.code) && $0.code >= 0 && $0.code <= 2147483647 && $0.signal == null)"

        let validSignal: bool =
            emitJsExpr
                (native, osConstants)
                "Boolean($0 && $0.code == null && typeof $0.signal === 'string' && Object.hasOwn($1.signals, $0.signal) && $0.signal !== 'SIGSTOP' && $0.signal !== 'SIGCONT')"

        if validCode then
            EndEvidence.Confirmed(ProgramEnd.Exited(unbox<int> (native?code)))
        elif validSignal then
            EndEvidence.Confirmed(ProgramEnd.Signaled(unbox<string> (native?signal)))
        else
            unknownEnd native failure

    let private rememberInspectionFailure (state: obj) failure =
        let nativeFailure: obj = state?failure

        if not (isNull nativeFailure) then
            rememberFailure failure (string (nativeFailure?message))

    let private observeTarget session failure =
        let state: obj = emitJsExpr session "$0.inspect()"
        rememberInspectionFailure state failure
        readNativeEnd (state?exit) failure

    let private signalReceipt (result: obj) =
        if string (result?kind) = "Failed" then
            Error(string (result?error?message))
        else
            Ok()

    let private requestSessionSignal (started: obj) name =
        try
            signalReceipt (started?signal (name))
        with error ->
            Error error.Message

    let private finalizeStartedSession
        (started: obj)
        (exitedRef: bool ref)
        (onExited: (unit -> unit) ResizeArray)
        (failure: string option ref)
        (rejection: NoExecution option ref)
        : Result<ChildProcess, ProcessResult> =
        let ok = emitJsExpr started "Boolean($0 && $0.ok)"

        if not ok then
            let errText: string =
                emitJsExpr started "String(($0 && $0.error) || 'Failed to spawn process')"

            Error(
                ProcessResult.Observed
                    { End = EndEvidence.Unconfirmed
                      StopRequested = None
                      Output = None
                      Fault = Some errText }
            )
        else
            let sessionObj: obj = emitJsExpr started "$0.session"
            let killObj: obj = emitJsExpr started "$0.kill"

            Ok
                { Process = sessionObj
                  Observe = fun () -> observeTarget sessionObj failure
                  Kill = (fun () -> invokeKill killObj)
                  RequestSignal = requestSessionSignal started
                  Exited = exitedRef
                  OnExited = onExited
                  Failure = failure
                  Rejection = rejection }

    let private launchExecutableSession
        (executable: string)
        (cwd: string)
        (envObj: obj)
        (arguments: string list)
        (stdin: byte[] option)
        (onStdout: byte[] -> unit)
        (onStderr: byte[] -> unit)
        (keepInputOpen: bool)
        : Task<Result<ChildProcess, ProcessResult>> =
        task {
            // DSL-MUTABLE: algorithm-scratch — exit callback list accumulator
            let onExited = ResizeArray<unit -> unit>()
            // DSL-MUTABLE: cancellation — child process exit flag.
            let exitedRef = ref false
            let onClose = markProcessClosed exitedRef onExited
            let failure = ref None
            let rejection = ref None

            let started =
                startProcessSession
                    SupervisedProcessCtor
                    executable
                    (arguments |> List.toArray)
                    cwd
                    envObj
                    (stdinValue stdin)
                    onStdout
                    onStderr
                    onClose
                    (fun message ->
                        if failure.Value.IsNone then
                            failure.Value <- Some message)
                    (fun reason -> rejection.Value <- Some(NoExecution.StartRejected reason))
                    keepInputOpen

            return finalizeStartedSession started exitedRef onExited failure rejection
        }

    let private spawnOrReject
        (keepInputOpen: bool)
        (cmd: Command)
        (ctx: ProcessContext)
        (onStdout: byte[] -> unit)
        (onStderr: byte[] -> unit)
        =
        task {
            let envObj = buildEnv cmd.Environment
            let cwd = cwdValue cmd ctx

            let executable =
                resolveExecutablePath cmd.FileName cwd envObj pathJoin statSync accessSync

            if isNull executable then
                return Error(ProcessResult.NotStarted(NoExecution.InvalidRequest "Invalid executable name"))
            else
                let stdin = cmd.Stdin |> Option.map System.Text.Encoding.UTF8.GetBytes

                return!
                    launchExecutableSession executable cwd envObj cmd.Arguments stdin onStdout onStderr keepInputOpen
        }

    let spawn
        (cmd: Command)
        (ctx: ProcessContext)
        (onStdout: byte[] -> unit)
        (onStderr: byte[] -> unit)
        (ct: CancellationToken)
        : Task<Result<ChildProcess, ProcessResult>> =
        task {
            if ct.IsCancellationRequested then
                return Error(ProcessResult.NotStarted NoExecution.CancelledBeforeStart)
            else
                return! spawnOrReject false cmd ctx onStdout onStderr
        }

    let spawnInteractive cmd ctx onStdout onStderr (ct: CancellationToken) =
        task {
            if ct.IsCancellationRequested then
                return Error(ProcessResult.NotStarted NoExecution.CancelledBeforeStart)
            elif cmd.Stdin.IsSome then
                return
                    Error(ProcessResult.NotStarted(NoExecution.InvalidRequest "Interactive input must use writeInput"))
            else
                return! spawnOrReject true cmd ctx onStdout onStderr
        }

    let private inputReceipt (bytes: byte[]) (result: obj) =
        let confirmed: bool =
            emitJsExpr (result, bytes.Length) "Boolean($0 && $0.kind === 'Written' && $0.bytes === $1)"

        if confirmed then
            Ok bytes.Length
        else
            Error "Input delivery was not confirmed; do not resend"

    let private writeStartedInput (child: ChildProcess) (bytes: byte[]) (ct: CancellationToken) (started: obj) =
        task {
            if ct.IsCancellationRequested then
                return Error "Input was cancelled before write"
            elif string (started?kind) <> "Started" then
                return Error "Target start was not confirmed"
            else
                let! result = unbox<Task<obj>> (child.Process?write(bytes))
                return inputReceipt bytes result
        }

    let private awaitInputStart (child: ChildProcess) bytes (ct: CancellationToken) =
        task {
            if ct.IsCancellationRequested then
                return Error "Input was cancelled before write"
            else
                let! started = unbox<Task<obj>> (child.Process?started())
                return! writeStartedInput child bytes ct started
        }

    let writeInput (child: ChildProcess) (bytes: byte[]) (ct: CancellationToken) : Task<Result<int, string>> =
        task {
            try
                return! awaitInputStart child bytes ct
            with error ->
                return Error error.Message
        }

    let signal (child: ChildProcess) (name: string) : Result<unit, string> = child.RequestSignal name

    let spawnBinary fileName arguments (workingDirectory: string) stdin onStdout onStderr =
        task {
            let envObj = buildEnv None
            let cwd = resolveAbsoluteCwd (box workingDirectory) pathJoin

            let executable =
                resolveExecutablePath fileName cwd envObj pathJoin statSync accessSync

            if isNull executable then
                return Error(ProcessResult.NotStarted(NoExecution.InvalidRequest "Invalid executable name"))
            else
                return! launchExecutableSession executable cwd envObj arguments stdin onStdout onStderr false
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
