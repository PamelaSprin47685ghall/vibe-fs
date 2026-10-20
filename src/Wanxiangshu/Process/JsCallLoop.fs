namespace Wanxiangshu.Process

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Host.Contract
open Wanxiangshu.Repository.Programming.Js

/// Physical execution and control flow for js.call.
module JsCallLoop =
    type ExecutionScope =
        { Cancellation: CancellationToken
          HardLimit: TimeSpan
          SynchronousBudgetMs: int
          OutputNotice: unit -> string
          ReadOutput: unit -> Task<Result<unit, string>>
          BorrowDeadline: int64 -> Task<(unit -> unit)>
          RemainingMs: unit -> Task<int64>
          Own: (unit -> Task) -> bool }

    let standalone =
        { Cancellation = CancellationToken.None
          HardLimit = ProcessEstimate.DefaultHardLimit
          SynchronousBudgetMs = 10000
          OutputNotice = fun () -> ""
          ReadOutput = fun () -> Task.FromResult(Ok())
          BorrowDeadline = fun _ -> Task.FromResult ignore
          RemainingMs = fun () -> Task.FromResult 2147483647L
          Own =
            fun start ->
                start () |> ignore
                true }

    [<Import("connect", "node:net")>]
    let private netConnect: obj = jsNative

    [<Emit("(function(connectFn, port) { return new Promise(function(resolve) { const socket = connectFn({ port: port, host: '127.0.0.1' }); let done = false; const finish = value => { if (done) return; done = true; clearTimeout(timer); socket.destroy(); resolve(value); }; const timer = setTimeout(() => finish(false), 100); socket.once('connect', () => finish(true)); socket.once('error', () => finish(false)); }); })($0, $1)")>]
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

    [<Emit("""
      (function(deadline, elapsed) {
        let stopped = false;
        let timer;
        function step() {
          if (stopped) return;
          const remaining = Number(deadline - BigInt(Math.floor(performance.now())));
          if (remaining <= 0) elapsed();
          else timer = setTimeout(step, Math.min(2147483647, remaining));
        }
        step();
        return () => { stopped = true; clearTimeout(timer); };
      })($0, $1)
    """)>]
    let private scheduleDeadline (deadline: int64) (elapsed: unit -> unit) : unit -> unit = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : obj = jsNative

    [<Import("runInNewContext", "node:vm")>]
    let private runInContext (code: string) (context: obj) (options: obj) : obj = jsNative

    let private boundedCall code bindings remaining =
        if remaining <= 0 then
            raise (TimeoutException "Callable callback deadline exceeded")

        runInContext code bindings (createObj [ "timeout" ==> remaining ])

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

    let private callMethod instance methodName argument remaining =
        boundedCall
            "target[method](argument)"
            (createObj [ "target" ==> instance; "method" ==> methodName; "argument" ==> argument ])
            remaining

    let private invokeThunk thunk remaining =
        boundedCall "thunk()" (createObj [ "thunk" ==> thunk ]) remaining

    [<Emit("Array.isArray($0)")>]
    let private isArray (value: obj) : bool = jsNative

    let private applyArguments instance methodName arguments remaining =
        boundedCall
            "Reflect.apply(target[method], target, args)"
            (createObj [ "target" ==> instance; "method" ==> methodName; "args" ==> arguments ])
            remaining

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
        // DSL-MUTABLE: resource — bounded raw head and tail for one output stream
        let mutable head: byte[] = [||]
        let mutable tail: byte[] = [||]
        let mutable totalBytes = 0L

        member _.TotalBytes = totalBytes

        member _.Truncated = totalBytes > int64 (head.Length + tail.Length)

        member this.Text =
            if this.Truncated then
                let prefix: string =
                    emitJsExpr head "new TextDecoder('utf-8', { ignoreBOM: true }).decode($0, { stream: true })"

                prefix + ProcessOutput.decodeText (Spool.alignUtf8Tail tail)
            else
                ProcessOutput.decodeText (Array.append head tail)

        member _.AppendBytes(chunk: byte[]) =
            totalBytes <- totalBytes + int64 chunk.Length
            let prefixCount = min chunk.Length (outputHeadLimit - head.Length)

            if prefixCount > 0 then
                head <- Array.append head (Array.sub chunk 0 prefixCount)

            if prefixCount < chunk.Length then
                let remaining = Array.sub chunk prefixCount (chunk.Length - prefixCount)
                tail <- Spool.retainLatestBytes outputTailLimit tail remaining

        member this.Append(chunk: string) =
            this.AppendBytes(System.Text.Encoding.UTF8.GetBytes chunk)

        member _.Clear() =
            head <- [||]
            tail <- [||]
            totalBytes <- 0L

    type private SliceBuffer() =
        let decoder: obj = emitJsExpr () "new TextDecoder('utf-8', { ignoreBOM: true })"
        // DSL-MUTABLE: resource — bounded complete characters since the last observation
        let mutable pending = OutputBuffer()

        member _.AppendBytes(chunk: byte[]) =
            let text: string = emitJsExpr (decoder, chunk) "$0.decode($1, { stream: true })"
            pending.Append text

        member _.Finish() =
            let text: string = emitJsExpr decoder "$0.decode()"
            pending.Append text

        member _.Take() =
            let taken = pending.Text
            pending <- OutputBuffer()
            taken

        member _.Clear() = pending.Clear()

    type private ManagedChild(name: string) =
        member _.Name = name
        member val Child: NodeProcessHost.ChildProcess option = None with get, set
        member val Exit = TaskCompletionSource<int>() with get
        member val Exited = false with get, set
        member val StartFailure: ProcessResult option = None with get, set
        member val RetireCapture = false with get, set
        member val RequiresOutputAuthority = false with get, set
        member val ObservedBytes = 0L with get, set
        member val SignalFailure: string option = None with get, set
        member val Stdout = OutputBuffer() with get
        member val Stderr = OutputBuffer() with get
        member val SliceStdout = SliceBuffer() with get
        member val SliceStderr = SliceBuffer() with get

        member val StdoutCapture =
            ProcessOutput.createWithOutputLimit (int64 (outputHeadLimit + outputTailLimit)) with get

        member val StderrCapture =
            ProcessOutput.createWithOutputLimit (int64 (outputHeadLimit + outputTailLimit)) with get

        member this.End =
            this.Child
            |> Option.map (fun child -> child.Observe())
            |> Option.defaultValue EndEvidence.Unconfirmed

        member this.ExitCode =
            match this.End with
            | EndEvidence.Confirmed(ProgramEnd.Exited code) -> Some code
            | _ -> None

        member this.Signal =
            match this.End with
            | EndEvidence.Confirmed(ProgramEnd.Signaled signal) -> Some signal
            | _ -> None

    let private releaseCapture (managed: ManagedChild) =
        ProcessOutput.discard managed.StdoutCapture
        ProcessOutput.discard managed.StderrCapture
        managed.Stdout.Clear()
        managed.Stderr.Clear()
        managed.SliceStdout.Clear()
        managed.SliceStderr.Clear()

    let private nowMs () : int64 =
        emitJsExpr () "BigInt(Math.floor(performance.now()))"

    let private callbackBudget scope deadline =
        int (min (int64 scope.SynchronousBudgetMs) (max 0L (deadline - nowMs ())))

    /// Fable's TaskCompletionSource does not expose TrySet*; a guarded
    /// SetResult is idempotent after the first completion.
    let private trySetResult (completion: TaskCompletionSource<int>) (value: int) =
        try
            completion.SetResult value
            true
        with _ ->
            false

    let private releaseRetiredCapture (managed: ManagedChild) =
        if managed.RetireCapture then
            releaseCapture managed

    let private closeManagedChild (managed: ManagedChild) =
        if not managed.Exited then
            managed.SliceStdout.Finish()
            managed.SliceStderr.Finish()
            managed.Exited <- true
            releaseRetiredCapture managed
            trySetResult managed.Exit 0 |> ignore

    let private observeManagedChild
        (cancellation: CancellationToken)
        (managed: ManagedChild)
        (child: NodeProcessHost.ChildProcess)
        closed
        =
        managed.Child <- Some child
        let registration = cancellation.Register(fun () -> child.Kill())
        let detachCancellation () = (registration :> IDisposable).Dispose()
        child.OnExited.Add detachCancellation
        child.OnExited.Add closed

        if child.Exited.Value then
            detachCancellation ()
            closed ()

    let private failure (code: string) (reason: string) : obj =
        createObj [ "ok" ==> false; "code" ==> code; "reason" ==> reason ]

    let private abandoned () : exn =
        emitJsExpr () "Object.assign(new Error('__jsAbandoned'), { __jsAbandoned: true })"

    let private outcome
        (status: string)
        (exitCode: int option)
        (signal: string option)
        (scopeClosed: bool)
        (fault: string option)
        (stopRequested: string option)
        (stdout: string)
        (stderr: string)
        (truncated: bool)
        (bytes: int64)
        (durationMs: int64)
        (steps: int)
        (reason: string)
        : obj =
        createObj
            [ "ok" ==> true
              "status" ==> status
              "reason" ==> (if isNull reason then null else box reason)
              "exitCode"
              ==> (match exitCode with
                   | Some code -> box code
                   | None -> null)
              "signal" ==> (signal |> Option.map box |> Option.defaultValue null)
              "scopeClosed" ==> scopeClosed
              "endUnconfirmed" ==> (exitCode.IsNone && signal.IsNone)
              "outputComplete" ==> (scopeClosed && fault.IsNone)
              "fault" ==> (fault |> Option.map box |> Option.defaultValue null)
              "stopRequested"
              ==> (stopRequested |> Option.map box |> Option.defaultValue null)
              "stdout" ==> stdout
              "stderr" ==> stderr
              "truncated" ==> truncated
              "bytes" ==> float bytes
              "durationMs" ==> float durationMs
              "steps" ==> float steps ]

    let private renderResult value =
        LlmFacing.instruction "ok"
        |> LlmFacing.withData (LlmFacing.Data.structuredValue (LlmFacing.Data.ofJs value))
        |> LlmFacing.render

    let private resultFits budget value =
        let wire = renderResult value
        int64 (LlmFacing.byteCount wire) <= budget && ToolResultBound.fitsInWindow wire

    let private headAndTail limit (text: string) =
        let bytes = System.Text.Encoding.UTF8.GetBytes text

        if bytes.Length <= limit then
            text
        elif limit <= 0 then
            ""
        else
            let headBytes = bytes |> Array.take (limit / 2)

            let tailBytes =
                bytes
                |> Array.skip (bytes.Length - (limit - headBytes.Length))
                |> Spool.alignUtf8Tail

            let head: string =
                emitJsExpr headBytes "new TextDecoder('utf-8', { ignoreBOM: true }).decode($0, { stream: true })"

            head + ProcessOutput.decodeText tailBytes

    let private minimumResult root notice =
        let sample =
            outcome
                "aborted"
                (Some Int32.MaxValue)
                (Some "SIGTERM")
                false
                (Some "…")
                (Some "cancellation")
                ""
                ""
                true
                9007199254740991L
                9007199254740991L
                Int32.MaxValue
                "…"

        sample?outputNotice <- notice
        sample?diagnosticTruncated <- true
        sample?notStarted <- true
        sample?root <- root
        sample?elapsedMs <- 9007199254740991.0
        renderResult sample

    let private minimumOutputBudget root notice =
        int64 (LlmFacing.byteCount (minimumResult root notice))

    let private boundResult budget notice (value: obj) =
        let originalStdout = string value?stdout
        let originalStderr = string value?stderr
        let originalReason = if isNull value?reason then "" else string value?reason
        let originalFault = if isNull value?fault then "" else string value?fault
        let mutable lower = 0

        let mutable upper =
            utf8Bytes originalStdout
            + utf8Bytes originalStderr
            + utf8Bytes originalReason
            + utf8Bytes originalFault

        let mutable best: obj = null

        let candidate capacity =
            let outputCapacity =
                max 0 (capacity - min capacity (utf8Bytes originalReason + utf8Bytes originalFault))

            let totalOutput = utf8Bytes originalStdout + utf8Bytes originalStderr

            let stdoutCapacity =
                if totalOutput = 0 then
                    0
                else
                    int (float outputCapacity * float (utf8Bytes originalStdout) / float totalOutput)

            let reason = headAndTail (min capacity (utf8Bytes originalReason)) originalReason
            let fault = headAndTail (max 0 (capacity - utf8Bytes reason)) originalFault
            let copy: obj = emitJsExpr value "Object.assign({}, $0)"
            copy?stdout <- headAndTail stdoutCapacity originalStdout
            copy?stderr <- headAndTail (outputCapacity - stdoutCapacity) originalStderr

            if not (isNull value?reason) then
                copy?reason <- reason

            if not (isNull value?fault) then
                copy?fault <- fault

            copy?diagnosticTruncated <- (reason <> originalReason || fault <> originalFault)

            copy?truncated <-
                (unbox<bool> value?truncated
                 || string copy?stdout <> originalStdout
                 || string copy?stderr <> originalStderr)

            copy?outputNotice <- (if unbox<bool> copy?truncated then notice else "")
            copy

        let searchCapacity () =
            let capacity = lower + (upper - lower) / 2
            let current = candidate capacity

            if resultFits budget current then
                best <- current
                lower <- capacity + 1
            else
                upper <- capacity - 1

        while lower <= upper do
            searchCapacity ()

        best

    let private awaitJs (value: obj) : Task<obj> = unbox<Task<obj>> (promiseOf value)

    let private startChild scope root name command interactive onStdout onStderr : Task<ManagedChild> =
        task {
            let managed = ManagedChild(name)

            let request =
                { FileName = "sh"
                  Arguments = [ "-lc"; command ]
                  WorkingDirectory = Some root
                  Environment = None
                  Stdin = None }

            let context =
                { WorkingDirectory = Some root
                  HardLimit = scope.HardLimit }

            let captureStdout (bytes: byte[]) =
                managed.ObservedBytes <- managed.ObservedBytes + int64 bytes.Length

                try
                    ProcessOutput.addStdout managed.StdoutCapture bytes
                finally
                    managed.RequiresOutputAuthority <-
                        managed.RequiresOutputAuthority || managed.StdoutCapture.Spool.IsSome

                managed.Stdout.AppendBytes bytes
                managed.SliceStdout.AppendBytes bytes
                onStdout bytes

            let captureStderr (bytes: byte[]) =
                managed.ObservedBytes <- managed.ObservedBytes + int64 bytes.Length

                try
                    ProcessOutput.addStderr managed.StderrCapture bytes
                finally
                    managed.RequiresOutputAuthority <-
                        managed.RequiresOutputAuthority || managed.StderrCapture.Spool.IsSome

                managed.Stderr.AppendBytes bytes
                managed.SliceStderr.AppendBytes bytes
                onStderr bytes

            let closed () = closeManagedChild managed

            let spawn =
                if interactive then
                    NodeProcessHost.spawnInteractive
                else
                    NodeProcessHost.spawn

            let! started = spawn request context captureStdout captureStderr scope.Cancellation

            match started with
            | Error failure ->
                managed.StartFailure <- Some failure
                closed ()
            | Ok child -> observeManagedChild scope.Cancellation managed child closed

            return managed
        }

    let private killManaged (managed: ManagedChild) (signal: string) =
        managed.Child
        |> Option.iter (fun child ->
            match NodeProcessHost.signal child signal with
            | Ok() -> ()
            | Error reason -> managed.SignalFailure <- Some reason)

    let private childView (managed: ManagedChild) : obj =
        createObj
            [ "name" ==> managed.Name
              "exited" ==> managed.Exited
              "exitCode" ==> (managed.ExitCode |> Option.map box |> Option.defaultValue null)
              "signal" ==> (managed.Signal |> Option.map box |> Option.defaultValue null)
              "scopeClosed" ==> managed.Exited ]

    let private childWasNotStarted (managed: ManagedChild) =
        match managed.StartFailure with
        | Some(ProcessResult.NotStarted _) -> true
        | _ -> managed.Child |> Option.exists (fun child -> child.Rejection.Value.IsSome)

    let private retireCapture (managed: ManagedChild) =
        managed.RetireCapture <- true

        if managed.Exited then
            releaseCapture managed

    let private liveChildren (owned: seq<ManagedChild>) =
        owned |> Seq.filter (fun child -> not child.Exited)

    let private signalLiveChildren signal owned =
        for child in liveChildren owned do
            killManaged child signal

    /// Wait until the child exits or one time slice elapses. Returns true when
    /// the child has exited.
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

    /// Await exit after a kill, up to the grace window.
    let private awaitExit (managed: ManagedChild) (graceMs: int) : Task<unit> =
        task {
            let deadline = nowMs () + int64 graceMs

            while not managed.Exited && nowMs () < deadline do
                let remaining = max 1 (int (deadline - nowMs ()))
                let! _ = waitSlice managed (min 250 remaining)
                ()
        }

    let private awaitChildrenUntil deadline owned =
        task {
            for child in liveChildren owned do
                do! awaitExit child (max 0 (int (deadline - nowMs ())))
        }

    /// Race one host call against the remaining run budget. `None` means the
    /// budget expired first.
    let private classifyRaceWinner (winner: obj) : obj option =
        if hasProperty winner "timeout" then
            None
        elif hasProperty winner "error" then
            raise (System.Exception(string (getProperty winner "error")))
        else
            Some(getProperty winner "value")

    let private raceDeadline (cancellation: CancellationToken) (work: Task<obj>) (timeoutMs: int) : Task<obj option> =
        if timeoutMs <= 0 then
            Task.FromResult None
        else
            task {
                let cancelled = TaskCompletionSource<obj>()
                let cancelledValue = obj ()

                use registration =
                    cancellation.Register(fun () -> cancelled.SetResult cancelledValue)

                let raced: obj = emitJsExpr (work, cancelled.Task) "Promise.race([$0, $1])"
                let! winner = unbox<Task<obj>> (raceWorkTimeout raced timeoutMs)

                return
                    classifyRaceWinner winner
                    |> Option.filter (fun value -> not (obj.ReferenceEquals(value, cancelledValue)))
            }

    type private Verdict =
        | Continue
        | Abort of string
        | Park of obj

    let private verdictOf (raw: obj) : Verdict =
        if isNullish raw then
            Continue
        elif typeOf raw = "boolean" && not (unbox<bool> raw) then
            Abort "watch returned false"
        elif typeOf raw = "boolean" then
            Continue
        elif hasProperty raw "abort" && not (isNullish (getProperty raw "abort")) then
            Abort(string (getProperty raw "abort"))
        elif hasProperty raw "park" then
            Park(getProperty raw "park")
        else
            Continue

    let private dispatchParkSignal
        (state: Wanxiangshu.Process.JsSandbox.ParkState)
        (stack: string)
        (payload: obj)
        : unit =
        state.ParkRequested <- true
        state.ParkPayload <- payload

        if not (isNull stack) then
            state.ParkStack <- stack

        if not (isNull state.ParkSignalResolve) then
            emitJsExpr (state.ParkSignalResolve, payload) "$0($1)" |> ignore

    let private interpretInjectedResume (injected: obj) : string =
        if isString injected && string injected = "abort" then
            "abort"
        else
            "continue"

    type private SliceAction =
        | SliceContinue
        | SliceAbort of string
        | SlicePark of obj

    let private sliceActionOf (raw: obj) : SliceAction =
        match verdictOf raw with
        | Continue -> SliceContinue
        | Abort reason -> SliceAbort reason
        | Park payload -> SlicePark payload

    type private ForegroundSliceTransition =
        | KeepWaiting
        | CompletedNormal
        | CompletedTimeout
        | TerminatedAbort of string
        | TerminatedParkAbort
        | TerminatedUnsupported

    let private interpretForegroundParkDecision (decision: string) : ForegroundSliceTransition =
        match decision with
        | "abort" -> TerminatedParkAbort
        | "unsupported" -> TerminatedUnsupported
        | _ -> KeepWaiting

    type private StepCommand =
        | EndOfRun
        | RunCommand of string
        | SpawnChild of name: string * command: string
        | SignalChild of name: string * signal: string
        | SendChild of name: string * input: string
        | WaitForPort of port: int * timeoutMs: int
        | InvalidCommand

    let private textField raw name =
        let value = getProperty raw name

        if isString value && not (String.IsNullOrWhiteSpace(string value)) then
            Some(string value)
        else
            None

    let private integerField raw name maximum =
        let value = getProperty raw name

        if
            isFiniteNumber value
            && unbox<float> value > 0.0
            && unbox<float> value <= maximum
            && floor (unbox<float> value) = unbox<float> value
        then
            Some(int (unbox<float> value))
        else
            None

    let private parseCommandString raw =
        if String.IsNullOrWhiteSpace(string raw) then
            InvalidCommand
        else
            RunCommand(string raw)

    let private parseSpawn raw =
        match textField raw "name", textField raw "spawn" with
        | Some name, Some command -> SpawnChild(name, command)
        | _ -> InvalidCommand

    let private parseSignal raw =
        match textField raw "name", textField raw "signal" with
        | Some name, Some signal when List.contains signal [ "SIGTERM"; "SIGKILL"; "SIGINT"; "SIGHUP" ] ->
            SignalChild(name, signal)
        | _ -> InvalidCommand

    let private parseNestedSend raw send =
        let input = getProperty send "input"

        let name =
            if hasProperty send "name" then
                textField send "name"
            else
                textField raw "name"

        match name with
        | Some name when isString input -> SendChild(name, string input)
        | _ -> InvalidCommand

    let private parseSend raw =
        let send = getProperty raw "send"

        let flatSend () =
            match textField raw "name" with
            | Some name -> SendChild(name, string send)
            | None -> InvalidCommand

        match typeOf send with
        | "string" -> flatSend ()
        | "object" when not (isNullish send) -> parseNestedSend raw send
        | _ -> InvalidCommand

    let private parsePortWait waitFor =
        let timeout =
            if isNullish (getProperty waitFor "timeoutMs") then
                Some waitForDefaultMs
            else
                integerField waitFor "timeoutMs" 2147483647.0

        match integerField waitFor "port" 65535.0, timeout with
        | Some port, Some duration -> WaitForPort(port, duration)
        | _ -> InvalidCommand

    let private parseWaitFor raw =
        let waitFor = getProperty raw "waitFor"

        if isNullish waitFor then
            InvalidCommand
        else
            parsePortWait waitFor

    let private parseStepCommand (raw: obj) : StepCommand =
        if isNullish raw then
            EndOfRun
        elif isString raw then
            parseCommandString raw
        elif
            [ "spawn"; "signal"; "send"; "waitFor" ]
            |> List.filter (hasProperty raw)
            |> List.length
            <> 1
        then
            InvalidCommand
        elif hasProperty raw "spawn" then
            parseSpawn raw
        elif hasProperty raw "signal" then
            parseSignal raw
        elif hasProperty raw "send" then
            parseSend raw
        elif hasProperty raw "waitFor" then
            parseWaitFor raw
        else
            InvalidCommand

    type private PortProbeOutcome =
        | PortReady
        | PortTimedOut
        | PortDeadline
        | PortAborted
        | PortUnsupported
        | PortPending

    let private decidePortProbe
        (connected: bool)
        (elapsed: int64)
        (timeoutMs: int64)
        (deadlineReached: bool)
        : PortProbeOutcome =
        if connected then PortReady
        elif elapsed >= timeoutMs then PortTimedOut
        elif deadlineReached then PortDeadline
        else PortPending

    let private interpretPortVerdict (verdict: SliceAction) (decision: string) : PortProbeOutcome =
        match verdict with
        | SliceAbort _ -> PortAborted
        | SlicePark _ when decision = "abort" -> PortAborted
        | SlicePark _ when decision = "unsupported" -> PortUnsupported
        | _ -> PortPending

    type private CallableValidated =
        { Class: obj
          Instance: obj
          MaxDurationMs: int64
          IntervalMs: int
          OutputBudgetBytes: int64
          WorldLock: bool
          HasOnExit: bool }

    let private parseIntervalMs (intervalValue: obj) : int =
        if isNullish intervalValue || not (isFiniteNumber intervalValue) then
            defaultIntervalMs
        else
            max minimumIntervalMs (int (unbox<float> intervalValue))

    let private outputBudgetOrDefault budget =
        if isNullish budget then
            65536L
        else
            int64 (unbox<float> budget)

    let private validateCallableSpec (runnableClass: obj) : Result<CallableValidated, obj> =
        if not (isFunction runnableClass) then
            Error(failure "CALLABLE_SPEC_INCOMPLETE" "js.call expects a class extending js.Callable")
        else
            let instance = construct runnableClass
            let maxDurationValue = getProperty instance "maxDuration"
            let hasNext = isFunction (getProperty instance "next")
            let hasWatch = isFunction (getProperty instance "watch")
            let intervalValue = getProperty instance "interval"
            let intervalMs = parseIntervalMs intervalValue
            let budget = getProperty instance "outputBudgetBytes"
            let worldLock = getProperty instance "worldLock"

            if
                isNullish maxDurationValue
                || not (isFiniteNumber maxDurationValue)
                || unbox<float> maxDurationValue <= 0.0
            then
                Error(failure "CALLABLE_SPEC_INCOMPLETE" "Callable must declare a positive maxDuration")
            elif not hasNext then
                Error(failure "CALLABLE_SPEC_INCOMPLETE" "Callable must implement next(ctx)")
            elif not hasWatch then
                Error(failure "CALLABLE_SPEC_INCOMPLETE" "Callable must implement watch(ctx)")
            elif not (isFunction (getProperty instance "sanity")) then
                Error(
                    failure
                        "CALLABLE_SPEC_INCOMPLETE"
                        "Callable must implement sanity() — a control-flow self-test returning argument-array thunks"
                )
            elif
                not (isNullish budget)
                && (not (isFiniteNumber budget)
                    || unbox<float> budget < 0.0
                    || unbox<float> budget > 9007199254740991.0
                    || floor (unbox<float> budget) <> unbox<float> budget)
            then
                Error(failure "CALL_OUTPUT_BUDGET_INVALID" "outputBudgetBytes must be a non-negative safe integer")
            elif not (isNullish worldLock) && typeOf worldLock <> "boolean" then
                Error(failure "CALL_WORLD_LOCK_INVALID" "worldLock must be boolean")
            else
                let maxDurationMs = int64 (unbox<float> maxDurationValue)
                let hasOnExit = isFunction (getProperty instance "onExit")

                Ok
                    { Class = runnableClass
                      Instance = instance
                      MaxDurationMs = maxDurationMs
                      IntervalMs = intervalMs
                      OutputBudgetBytes = outputBudgetOrDefault budget
                      WorldLock = not (isNullish worldLock) && unbox<bool> worldLock
                      HasOnExit = hasOnExit }

    let private requireLivePark (state: Wanxiangshu.Process.JsSandbox.ParkState) =
        if state.AbandonRequested then
            raise (abandoned ())

    /// Suspend the run through the shared park channel and wait for the
    /// injected value. The injected string 'abort' aborts; anything else
    /// resumes the loop. Returns "unsupported" when the run has no park state.
    let private parkAtWatch
        (parkState: Wanxiangshu.Process.JsSandbox.ParkState option)
        (stack: string)
        (payload: obj)
        : Task<string> =
        task {
            if parkState.IsNone then
                return "unsupported"
            else
                let state = parkState.Value

                requireLivePark state

                dispatchParkSignal state stack payload

                let deferred: obj =
                    emitJsExpr
                        state
                        "(function(state) { return new Promise(function(resolve, reject) { state.ParkDeferredResolve = resolve; state.ParkDeferredReject = reject; }); })($0)"

                let! injected = awaitJs deferred
                return interpretInjectedResume injected
        }

    /// A failed prove() is a semantic fork, not a terminal failure: suspend
    /// through the shared park channel with the diagnosis payload, and let the
    /// injected unpark value become the js.call outcome by this park point's
    /// contract. Without a park channel the diagnosis cannot be presented, so
    /// the run refuses instead of silently degrading.
    let private parkOrFailProof
        (parkState: Wanxiangshu.Process.JsSandbox.ParkState option)
        (stack: string)
        (payload: obj)
        : Task<obj> =
        match parkState with
        | None ->
            Task.FromResult(
                failure "CALL_PARK_UNSUPPORTED" "prove failure requires the park channel to present its diagnosis"
            )
        | Some state ->
            task {
                requireLivePark state

                dispatchParkSignal state stack payload

                let deferred: obj =
                    emitJsExpr
                        state
                        "(function(state) { return new Promise(function(resolve, reject) { state.ParkDeferredResolve = resolve; state.ParkDeferredReject = reject; }); })($0)"

                return! awaitJs deferred
            }

    let private executeValidated
        (scope: ExecutionScope)
        (parkState: Wanxiangshu.Process.JsSandbox.ParkState option)
        (root: string)
        (stack: string)
        (spec: CallableValidated)
        (owned: ResizeArray<ManagedChild>)
        (startedMs: int64)
        : Task<obj> =
        task {
            let instance = spec.Instance
            let maxDurationMs = min spec.MaxDurationMs (int64 scope.HardLimit.TotalMilliseconds)
            let intervalMs = spec.IntervalMs
            let hasOnExit = spec.HasOnExit
            let deadlineMs = startedMs + maxDurationMs
            let children = Dictionary<string, ManagedChild>()
            let stdout = OutputBuffer()
            let stderr = OutputBuffer()
            // // DSL-MUTABLE: algorithm-scratch — loop step counter shared with
            // watchContext (a ref cell: a closure cannot capture a mutable let)
            let stepCell = ref 0
            // DSL-MUTABLE: algorithm-scratch — overall loop state
            let mutable running = true
            let mutable status = "completed"
            // DSL-MUTABLE: algorithm-scratch — abort/timeout explanation
            let mutable outcomeReason: string = null
            let mutable lastExitCode: int option = None
            let mutable lastSignal: string option = None
            let mutable lastForeground: ManagedChild option = None
            // DSL-MUTABLE: algorithm-scratch — failure/outcome slot for this run
            let mutable result: obj = null
            let mutable outputFault: string option = None
            let mutable abandonedError: exn option = None
            let scratch = ManagedChild("wait")

            let requestOutputAuthority () =
                task {
                    try
                        let! permitted = scope.ReadOutput()
                        permitted |> Result.defaultWith (fun reason -> outputFault <- Some reason)
                    with error ->
                        outputFault <- Some error.Message
                }

            let authorizeOutput () =
                task {
                    if owned |> Seq.exists (fun entry -> entry.RequiresOutputAuthority) then
                        do! requestOutputAuthority ()
                }

            let startManaged name command interactive =
                task {
                    let! managed = startChild scope root name command interactive stdout.AppendBytes stderr.AppendBytes
                    owned.Add managed
                    return managed
                }

            let contextFields (managed: ManagedChild) output error backgrounds : obj =
                createObj
                    [ "root" ==> root
                      "step" ==> stepCell.Value
                      "stdout" ==> output
                      "stderr" ==> error
                      "exited" ==> managed.Exited
                      "exitCode" ==> (managed.ExitCode |> Option.map box |> Option.defaultValue null)
                      "signal" ==> (managed.Signal |> Option.map box |> Option.defaultValue null)
                      "scopeClosed" ==> managed.Exited
                      "backgrounds" ==> backgrounds
                      "elapsedMs" ==> float (nowMs () - startedMs)
                      "remainingMs" ==> float (max 0L (deadlineMs - nowMs ()))
                      "truncated" ==> false ]

            let watchContext (managed: ManagedChild) : obj =
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

                contextFields
                    managed
                    (if outputFault.IsNone then
                         String.concat "" stdoutParts
                     else
                         "")
                    (if outputFault.IsNone then
                         String.concat "" stderrParts
                     else
                         "")
                    (children.Values |> Seq.map childView |> Seq.toArray)
                |> boundResult spec.OutputBudgetBytes (scope.OutputNotice())

            let backgroundBudgetMinimum name =
                let backgrounds =
                    Seq.append children.Keys [ name ]
                    |> Seq.map (fun childName ->
                        createObj
                            [ "name" ==> childName
                              "exited" ==> false
                              "exitCode" ==> Int32.MaxValue
                              "signal" ==> "SIGTERM"
                              "scopeClosed" ==> false ])
                    |> Seq.toArray

                let preview = contextFields scratch "" "" backgrounds
                preview?exitCode <- Int32.MaxValue
                preview?signal <- "SIGTERM"
                preview?step <- Int32.MaxValue
                preview?elapsedMs <- 9007199254740991.0
                preview?remainingMs <- 9007199254740991.0
                preview?truncated <- true
                preview?diagnosticTruncated <- true
                preview?outputNotice <- scope.OutputNotice()
                int64 (LlmFacing.byteCount (renderResult preview))

            let exitContext (managed: ManagedChild) : obj =
                let fault =
                    outputFault
                    |> Option.orElse (managed.Child |> Option.bind (fun child -> child.Failure.Value))

                let view =
                    outcome
                        status
                        managed.ExitCode
                        managed.Signal
                        managed.Exited
                        fault
                        None
                        (if outputFault.IsNone then managed.Stdout.Text else "")
                        (if outputFault.IsNone then managed.Stderr.Text else "")
                        (managed.Stdout.Truncated || managed.Stderr.Truncated)
                        managed.ObservedBytes
                        (nowMs () - startedMs)
                        stepCell.Value
                        outcomeReason

                view?root <- root
                view?elapsedMs <- float (nowMs () - startedMs)
                view?outputComplete <- (managed.Exited && fault.IsNone && status = "completed")
                boundResult spec.OutputBudgetBytes (scope.OutputNotice()) view

            let interpretParkDecision (decision: string) : string =
                if decision = "unsupported" then
                    result <- failure "CALL_PARK_UNSUPPORTED" "park verdict requires the orchestrated execution path"

                    "unsupported"
                else
                    decision

            let parkOrAbort (payload: obj) : Task<string> =
                if not (isNull result) then
                    Task.FromResult "stop"
                else
                    task {
                        let! decision = parkAtWatch parkState stack payload
                        return interpretParkDecision decision
                    }

            let finishManaged (managed: ManagedChild) : Task<unit> =
                task {
                    if not managed.Exited then
                        let! _ = awaitExit managed signalGraceMs
                        ()
                }

            let watchForegroundSlice (managed: ManagedChild) : Task<ForegroundSliceTransition> =
                task {
                    do! authorizeOutput ()

                    let! verdictRaw =
                        raceDeadline
                            scope.Cancellation
                            (awaitJs (
                                callMethod instance "watch" (watchContext managed) (callbackBudget scope deadlineMs)
                            ))
                            (int (deadlineMs - nowMs ()))

                    match verdictRaw |> Option.map sliceActionOf with
                    | None -> return CompletedTimeout
                    | Some SliceContinue -> return KeepWaiting
                    | Some(SliceAbort reason) -> return TerminatedAbort reason
                    | Some(SlicePark payload) ->
                        let! decision = parkOrAbort payload
                        return interpretForegroundParkDecision decision
                }

            let foregroundSliceAfterWait (managed: ManagedChild) (exited: bool) : Task<ForegroundSliceTransition> =
                if exited then
                    Task.FromResult CompletedNormal
                else
                    watchForegroundSlice managed

            let stepForegroundSlice (managed: ManagedChild) : Task<ForegroundSliceTransition> =
                task {
                    let remaining = deadlineMs - nowMs ()

                    if scope.Cancellation.IsCancellationRequested then
                        return TerminatedAbort "Host cancellation requested"
                    elif remaining <= 0L then
                        return CompletedTimeout
                    else
                        let slice = int (min (int64 intervalMs) remaining)
                        let! exited = waitSlice managed slice
                        return! foregroundSliceAfterWait managed exited
                }

            let applyForegroundTransition
                (managed: ManagedChild)
                (transition: ForegroundSliceTransition)
                : Task<bool * bool> =
                task {
                    match transition with
                    | KeepWaiting -> return false, false
                    | CompletedNormal -> return true, false
                    | CompletedTimeout ->
                        status <- "timeout"
                        outcomeReason <- "maxDuration exceeded"
                        killManaged managed "SIGKILL"
                        do! finishManaged managed
                        return true, false
                    | TerminatedAbort reason ->
                        status <- "aborted"
                        outcomeReason <- reason
                        killManaged managed "SIGKILL"
                        do! finishManaged managed
                        return true, true
                    | TerminatedParkAbort ->
                        status <- "aborted"
                        outcomeReason <- "watch park resumed with 'abort'"
                        killManaged managed "SIGKILL"
                        do! finishManaged managed
                        return true, true
                    | TerminatedUnsupported ->
                        killManaged managed "SIGKILL"
                        do! finishManaged managed
                        return true, false
                }

            let settleForegroundChild (managed: ManagedChild) : Task<unit> =
                task {
                    lastExitCode <- managed.ExitCode
                    lastSignal <- managed.Signal
                    do! authorizeOutput ()

                    let! observed =
                        raceDeadline
                            scope.Cancellation
                            (awaitJs (
                                callMethod instance "onExit" (exitContext managed) (callbackBudget scope deadlineMs)
                            ))
                            (int (deadlineMs - nowMs ()))

                    if observed.IsNone then
                        status <- "timeout"
                        outcomeReason <- "maxDuration exceeded inside onExit(ctx)"
                }

            let runForeground (command: string) : Task<unit> =
                task {
                    let! managed = startManaged "foreground" command false
                    lastForeground <- Some managed
                    lastExitCode <- None
                    lastSignal <- None
                    // DSL-MUTABLE: algorithm-scratch — per-command loop state
                    let mutable finished = false
                    let mutable aborted = false

                    while not finished && isNull result do
                        let! transition = stepForegroundSlice managed
                        let! (isFinished, isAborted) = applyForegroundTransition managed transition
                        finished <- isFinished
                        aborted <- isAborted

                    if isNull result && hasOnExit then
                        do! settleForegroundChild managed
                    elif isNull result then
                        lastExitCode <- managed.ExitCode
                        lastSignal <- managed.Signal

                    if managed.Exited then
                        releaseCapture managed

                    if aborted || status = "timeout" then
                        running <- false
                }

            let escalateKill (managed: ManagedChild) : Task<unit> =
                task {
                    if not managed.Exited then
                        killManaged managed "SIGKILL"
                        let! _ = awaitExit managed signalGraceMs
                        ()
                }

            let finishChildSignal name (managed: ManagedChild) =
                if managed.Exited then
                    releaseCapture managed
                    children.Remove name |> ignore
                else
                    status <- "failed"
                    outcomeReason <- "Background scope closure was not confirmed"
                    lastExitCode <- managed.ExitCode
                    lastSignal <- managed.Signal
                    running <- false

            let signalChildProcess (name: string) (signalName: string) : Task<unit> =
                task {
                    match children.TryGetValue name with
                    | false, _ -> result <- failure "CALL_CHILD_UNKNOWN" ("signal names an unknown background: " + name)
                    | true, managed ->
                        killManaged managed signalName
                        let! _ = awaitExit managed signalGraceMs
                        do! escalateKill managed
                        finishChildSignal name managed
                }

            let normalizeChildInput (input: string) : string =
                if input = "" || input.EndsWith "\n" then
                    input
                else
                    input + "\n"

            let acceptInputReceipt delivered =
                match delivered |> Option.map unbox<Result<int, string>> with
                | Some(Ok _) -> ()
                | Some(Error reason) -> result <- failure "CALL_INPUT_UNCONFIRMED" reason
                | None ->
                    status <- "timeout"
                    outcomeReason <- "maxDuration exceeded while sending input"
                    running <- false

            let writeChildInput child input =
                task {
                    let payload = normalizeChildInput input |> System.Text.Encoding.UTF8.GetBytes
                    use cancellation = new CancellationTokenSource()
                    use registration = scope.Cancellation.Register(fun () -> cancellation.Cancel())
                    let remaining = int (max 0L (deadlineMs - nowMs ()))

                    if remaining = 0 then
                        cancellation.Cancel()

                    let timer = emitJsExpr (remaining, cancellation.Cancel) "setTimeout($1, $0)"

                    try
                        let work =
                            task {
                                let! receipt = NodeProcessHost.writeInput child payload cancellation.Token
                                return box receipt
                            }

                        let! delivered = raceDeadline cancellation.Token work remaining
                        acceptInputReceipt delivered
                    finally
                        cancellation.Cancel()
                        emitJsExpr timer "clearTimeout($0)" |> ignore
                }

            let sendToManagedChild (managed: ManagedChild) input =
                task {
                    match managed.Child with
                    | None -> result <- failure "CALL_INPUT_UNCONFIRMED" "Target start was not confirmed"
                    | Some child -> do! writeChildInput child input
                }

            let sendChildInput (name: string) (input: string) : Task<unit> =
                task {
                    match children.TryGetValue name with
                    | false, _ -> result <- failure "CALL_CHILD_UNKNOWN" ("send names an unknown background: " + name)
                    | true, managed -> do! sendToManagedChild managed input
                }

            let samplePortSlice () : Task<PortProbeOutcome> =
                task {
                    let slice = int (min (int64 intervalMs) (max 1L (deadlineMs - nowMs ())))
                    let! _ = unbox<Task<unit>> (delay (min slice probeIntervalMs))

                    let! verdictRaw =
                        raceDeadline
                            scope.Cancellation
                            (awaitJs (
                                callMethod instance "watch" (watchContext scratch) (callbackBudget scope deadlineMs)
                            ))
                            (int (deadlineMs - nowMs ()))

                    let verdict =
                        verdictRaw
                        |> Option.map sliceActionOf
                        |> Option.defaultValue (SliceAbort "watch deadline exceeded")

                    let! decision =
                        match verdict with
                        | SlicePark payload -> parkOrAbort payload
                        | _ -> Task.FromResult "continue"

                    return interpretPortVerdict verdict decision
                }

            let probePortSlice (port: int) (waitStarted: int64) (timeoutMs: int) : Task<PortProbeOutcome> =
                task {
                    let! connectedRaw =
                        raceDeadline
                            scope.Cancellation
                            (awaitJs (box (probePort netConnect port)))
                            (int (min (int64 timeoutMs) (deadlineMs - nowMs ())))

                    let connected = connectedRaw |> Option.map unbox<bool> |> Option.defaultValue false

                    let initialOutcome =
                        decidePortProbe connected (nowMs () - waitStarted) (int64 timeoutMs) (nowMs () >= deadlineMs)

                    if initialOutcome <> PortPending then
                        return initialOutcome
                    else
                        return! samplePortSlice ()
                }

            let waitForPortCondition (port: int) (timeoutMs: int) : Task<unit> =
                task {
                    let waitStarted = nowMs ()
                    // DSL-MUTABLE: algorithm-scratch — waitFor poll state
                    let mutable ready = false
                    let mutable failureReason: string option = None

                    let applyPortOutcome (outcome: PortProbeOutcome) : unit =
                        match outcome with
                        | PortReady -> ready <- true
                        | PortTimedOut -> failureReason <- Some "timeout"
                        | PortDeadline ->
                            status <- "timeout"
                            outcomeReason <- "maxDuration exceeded"
                            failureReason <- Some "deadline"
                        | PortAborted -> failureReason <- Some "abort"
                        | PortUnsupported -> failureReason <- Some "unsupported"
                        | PortPending -> ()

                    while not ready && failureReason.IsNone && isNull result do
                        let! outcome = probePortSlice port waitStarted timeoutMs
                        applyPortOutcome outcome

                    if failureReason = Some "timeout" && isNull result then
                        result <- failure "CALL_WAIT_TIMEOUT" "waitFor condition was not met before its timeout"
                    elif failureReason = Some "abort" then
                        status <- "aborted"
                        outcomeReason <- "watch aborted while waiting for the port"
                        running <- false
                    elif failureReason = Some "deadline" || failureReason = Some "unsupported" then
                        running <- false
                }

            let spawnBackground name command =
                task {
                    if children.ContainsKey name then
                        result <- failure "CALL_CHILD_NAME_CONFLICT" ("A background already owns this name: " + name)
                    elif
                        backgroundBudgetMinimum name > min
                            spec.OutputBudgetBytes
                            (int64 (ToolResultBound.HostMaxBytes()))
                    then
                        result <-
                            failure
                                "CALL_OUTPUT_BUDGET_TOO_SMALL"
                                "outputBudgetBytes cannot encode the background observation"

                        result?minimumOutputBudgetBytes <- float (backgroundBudgetMinimum name)
                    else
                        let! managed = startManaged name command true
                        children.Add(name, managed)
                }

            let executeStepCommand (cmd: StepCommand) : Task<unit> =
                task {
                    match cmd with
                    | EndOfRun -> running <- false
                    | RunCommand c -> do! runForeground c
                    | SpawnChild(name, c) -> do! spawnBackground name c
                    | SignalChild(name, sigName) -> do! signalChildProcess name sigName
                    | SendChild(name, input) -> do! sendChildInput name input
                    | WaitForPort(port, timeoutMs) -> do! waitForPortCondition port timeoutMs
                    | InvalidCommand ->
                        result <-
                            failure
                                "CALL_COMMAND_UNSUPPORTED"
                                "next() must return a command string, null, { spawn, name }, { send: { name, input } }, { signal, name } or { waitFor: { port, timeoutMs } }"
                }

            let applyStepOutcome (stepOutcome: obj option) : Task<unit> =
                task {
                    match stepOutcome with
                    | None ->
                        status <- "timeout"
                        outcomeReason <- "maxDuration exceeded inside next(ctx)"
                        running <- false
                    | Some _ when scope.Cancellation.IsCancellationRequested -> running <- false
                    | Some _ when nowMs () >= deadlineMs ->
                        status <- "timeout"
                        outcomeReason <- "maxDuration exceeded before the physical step"
                        running <- false
                    | Some raw -> do! executeStepCommand (parseStepCommand raw)
                }

            let driveLoop () =
                task {
                    while running && isNull result && not scope.Cancellation.IsCancellationRequested do
                        let ctx = createObj [ "root" ==> root; "step" ==> stepCell.Value ]
                        let remaining = deadlineMs - nowMs ()

                        let! stepOutcome =
                            raceDeadline
                                scope.Cancellation
                                (awaitJs (callMethod instance "next" ctx (callbackBudget scope deadlineMs)))
                                (int remaining)

                        stepCell.Value <- stepCell.Value + 1
                        do! applyStepOutcome stepOutcome
                }

            let observeLoopFailure (error: exn) =
                status <- (if nowMs () >= deadlineMs then "timeout" else "failed")
                outcomeReason <- error.Message

                if hasProperty (box error) "__jsAbandoned" then
                    abandonedError <- Some error

            try
                do! driveLoop ()
            with error ->
                observeLoopFailure error

            if scope.Cancellation.IsCancellationRequested then
                status <- "aborted"
                outcomeReason <- "Host cancellation requested"

            let cleanupDeadline = nowMs () + int64 signalGraceMs
            signalLiveChildren "SIGTERM" owned
            do! awaitChildrenUntil cleanupDeadline owned
            signalLiveChildren "SIGKILL" owned
            let killDeadline = nowMs () + int64 signalGraceMs
            do! awaitChildrenUntil killDeadline owned

            lastForeground
            |> Option.iter (fun managed ->
                lastExitCode <- managed.ExitCode
                lastSignal <- managed.Signal)

            let scopeClosed = owned |> Seq.forall (fun entry -> entry.Exited)
            do! authorizeOutput ()

            let fault =
                owned
                |> Seq.tryPick (fun entry ->
                    entry.StartFailure
                    |> Option.map ProcessResult.describe
                    |> Option.orElse (
                        entry.Child
                        |> Option.bind (fun child -> child.Rejection.Value)
                        |> Option.map (ProcessResult.NotStarted >> ProcessResult.describe)
                    )
                    |> Option.orElse (entry.Child |> Option.bind (fun child -> child.Failure.Value))
                    |> Option.orElse entry.SignalFailure
                    |> Option.map (fun message -> entry.Name + ": " + message))
                |> Option.orElse (
                    if scopeClosed then
                        None
                    else
                        Some "Process scope closure was not confirmed"
                )
                |> Option.orElse outputFault

            if fault.IsSome && status = "completed" then
                status <- "failed"
                outcomeReason <- fault.Value

            for entry in owned do
                retireCapture entry

            if abandonedError.IsSome then
                return raise abandonedError.Value
            elif parkState |> Option.exists (fun state -> state.AbandonRequested) then
                return raise (abandoned ())
            elif not (isNull result) then
                return result
            else
                let value =
                    outcome
                        status
                        lastExitCode
                        lastSignal
                        scopeClosed
                        fault
                        (if scope.Cancellation.IsCancellationRequested then
                             Some "cancellation"
                         elif status = "timeout" then
                             Some "deadline"
                         elif status = "aborted" then
                             Some "watch"
                         else
                             None)
                        (if outputFault.IsNone then stdout.Text else "")
                        (if outputFault.IsNone then stderr.Text else "")
                        (stdout.Truncated || stderr.Truncated)
                        (owned |> Seq.sumBy (fun entry -> entry.ObservedBytes))
                        (nowMs () - startedMs)
                        stepCell.Value
                        outcomeReason

                let notStarted = owned |> Seq.forall childWasNotStarted
                value?notStarted <- notStarted
                value?endUnconfirmed <- (not notStarted && lastExitCode.IsNone && lastSignal.IsNone)
                value?outputComplete <- (not notStarted && scopeClosed && fault.IsNone && status = "completed")
                return boundResult spec.OutputBudgetBytes (scope.OutputNotice()) value
        }

    let private acquireLargePermit (cancellation: CancellationToken) duration =
        task {
            use queueCancellation = new CancellationTokenSource()
            use registration = cancellation.Register(fun () -> queueCancellation.Cancel())

            let timer =
                emitJsExpr (duration, queueCancellation.Cancel) "setTimeout($1, Number($0))"

            try
                do! LargeGate.acquire queueCancellation.Token
            finally
                emitJsExpr timer "clearTimeout($0)" |> ignore
        }

    let private awaitPhysicalClosure owned =
        task {
            for child in liveChildren owned do
                let! _ = child.Exit.Task
                ()
        }

    let private closeRetainedChild (child: ManagedChild) =
        task {
            child.RetireCapture <- true

            if not child.Exited then
                killManaged child "SIGKILL"
                let! _ = child.Exit.Task
                ()
        }

    let private closeRetainedChildren owned =
        task {
            for child in owned do
                do! closeRetainedChild child
        }

    let private executeOwned scope parkState root stack spec =
        let published = TaskCompletionSource<obj>()
        let owned = ResizeArray<ManagedChild>()
        let startedMs = nowMs ()

        let admitted =
            scope.Own(fun () ->
                task {
                    let mutable held = false

                    let deadline =
                        startedMs + min spec.MaxDurationMs (int64 scope.HardLimit.TotalMilliseconds)

                    let releaseExpiredPermit () =
                        if
                            held
                            && nowMs () >= deadline
                            && (owned |> Seq.forall (fun child -> child.Exited))
                        then
                            held <- false
                            LargeGate.release ()

                    let stopAtDeadline () =
                        signalLiveChildren "SIGKILL" owned

                        task {
                            do! awaitPhysicalClosure owned
                            releaseExpiredPermit ()
                        }
                        |> ignore

                    let cancelDeadline = scheduleDeadline deadline stopAtDeadline

                    let acquirePermit () =
                        task {
                            if spec.WorldLock then
                                let duration = min spec.MaxDurationMs (int64 scope.HardLimit.TotalMilliseconds)
                                do! acquireLargePermit scope.Cancellation duration
                                held <- true
                        }

                    let publishExecution () =
                        task {
                            do! acquirePermit ()

                            if scope.Cancellation.IsCancellationRequested then
                                published.SetResult(
                                    failure
                                        "CALL_CANCELLED_BEFORE_START"
                                        "Host cancellation was requested before execution"
                                )
                            elif
                                nowMs () - startedMs
                                >= min spec.MaxDurationMs (int64 scope.HardLimit.TotalMilliseconds)
                            then
                                published.SetResult(
                                    failure
                                        "CALL_DEADLINE_BEFORE_START"
                                        "The run deadline expired before physical execution"
                                )
                            else
                                let! value = executeValidated scope parkState root stack spec owned startedMs
                                published.SetResult value
                        }

                    let publishFailure (error: exn) =
                        let noStartCode =
                            if scope.Cancellation.IsCancellationRequested then
                                "CALL_CANCELLED_BEFORE_START"
                            else
                                "CALL_DEADLINE_BEFORE_START"

                        if hasProperty (box error) "__jsAbandoned" then
                            published.SetException error
                        elif owned.Count = 0 then
                            published.SetResult(failure noStartCode error.Message)
                        else
                            let latest = owned[owned.Count - 1]

                            published.SetResult(
                                outcome
                                    "failed"
                                    latest.ExitCode
                                    latest.Signal
                                    (owned |> Seq.forall (fun child -> child.Exited))
                                    (Some error.Message)
                                    None
                                    ""
                                    ""
                                    false
                                    0L
                                    (nowMs () - startedMs)
                                    0
                                    error.Message
                                |> boundResult spec.OutputBudgetBytes (scope.OutputNotice())
                            )

                    let publish () =
                        task {
                            try
                                do! publishExecution ()
                            with error ->
                                publishFailure error
                        }

                    let finishPermit () =
                        cancelDeadline ()

                        if held then
                            LargeGate.release ()

                    try
                        do! publish ()
                        do! closeRetainedChildren owned
                    finally
                        finishPermit ()
                }
                :> Task)

        if admitted then
            published.Task
        else
            Task.FromResult(failure "CALL_HOST_UNAVAILABLE" "The process owner is closing")

    let private startWithinProgramDeadline scope start =
        task {
            if scope.Cancellation.IsCancellationRequested then
                return failure "CALL_CANCELLED_BEFORE_START" "Host cancellation was requested before execution"
            else
                return! start ()
        }

    let private withProgramDeadline scope duration start =
        task {
            let! release = scope.BorrowDeadline duration

            try
                return! startWithinProgramDeadline scope start
            finally
                release ()
        }

    let private executeWithProof scope parkState root stack spec =
        let sanityPayload (message: string) (returned: obj) =
            createObj [ "sanity" ==> createObj [ "failure" ==> message; "returned" ==> returned ] ]

        let isVeto (verdict: obj) =
            (typeOf verdict = "boolean" && not (unbox<bool> verdict))
            || (hasProperty verdict "abort" && not (isNullish (getProperty verdict "abort")))

        let sanityDeadline =
            nowMs () + min spec.MaxDurationMs (int64 scope.HardLimit.TotalMilliseconds)

        let awaitSanity value =
            task {
                let! observed = raceDeadline scope.Cancellation (awaitJs value) (int (sanityDeadline - nowMs ()))

                match observed with
                | Some value -> return value
                | None -> return raise (TimeoutException "Callable sanity deadline exceeded")
            }

        let mutable proofPassed = false

        let proveWatch watchVeto =
            task {
                let! arguments = awaitSanity (invokeThunk watchVeto (callbackBudget scope sanityDeadline))

                let! verdict =
                    if isArray arguments then
                        awaitSanity (
                            applyArguments spec.Instance "watch" arguments (callbackBudget scope sanityDeadline)
                        )
                    else
                        Task.FromResult null

                if not (isArray arguments) || not (isVeto verdict) then
                    return!
                        parkOrFailProof
                            parkState
                            stack
                            (sanityPayload
                                (if isArray arguments then
                                     "watch has no veto path — the watchdog cannot say no"
                                 else
                                     "watchVeto must return the argument array for watch")
                                verdict)
                else
                    proofPassed <- true
                    return null
            }

        let proveNext nextDone watchVeto =
            task {
                let! arguments = awaitSanity (invokeThunk nextDone (callbackBudget scope sanityDeadline))

                let! result =
                    if isArray arguments then
                        awaitSanity (
                            applyArguments spec.Instance "next" arguments (callbackBudget scope sanityDeadline)
                        )
                    else
                        Task.FromResult null

                if not (isArray arguments) || not (isNullish result) then
                    return!
                        parkOrFailProof
                            parkState
                            stack
                            (sanityPayload
                                (if isArray arguments then
                                     "next has no null path — the termination condition does not exist yet"
                                 else
                                     "nextDone must return the argument array for next")
                                result)
                else
                    return! proveWatch watchVeto
            }

        let driveProof () =
            task {
                let! sanity = awaitSanity (callMethod spec.Instance "sanity" null (callbackBudget scope sanityDeadline))

                let readThunk name =
                    boundedCall
                        "target[name]"
                        (createObj [ "target" ==> sanity; "name" ==> name ])
                        (callbackBudget scope sanityDeadline)

                let nextDone = readThunk "nextDone"
                let watchVeto = readThunk "watchVeto"

                if not (isFunction nextDone) || not (isFunction watchVeto) then
                    return
                        failure
                            "CALLABLE_SPEC_INCOMPLETE"
                            "sanity() must return { nextDone, watchVeto } — two thunks returning argument arrays"
                else
                    return! proveNext nextDone watchVeto
            }

        let proofFailure (error: exn) =
            if hasProperty (box error) "__jsAbandoned" then
                task { return raise error }
            elif scope.Cancellation.IsCancellationRequested then
                Task.FromResult(
                    failure "CALL_CANCELLED_BEFORE_START" "Host cancellation was requested before execution"
                )
            elif
                error :? TimeoutException
                || string (getProperty (box error) "code") = "ERR_SCRIPT_EXECUTION_TIMEOUT"
            then
                Task.FromResult(failure "CALL_SANITY_TIMEOUT" error.Message)
            else
                parkOrFailProof parkState stack (sanityPayload error.Message null)

        let prove () =
            task {
                try
                    return! driveProof ()
                with error ->
                    return! proofFailure error
            }

        // Proof and execution use separate instances so mocked state cannot enter the real run.
        withProgramDeadline
            scope
            (2L * min spec.MaxDurationMs (int64 scope.HardLimit.TotalMilliseconds)
             + 2L * int64 signalGraceMs)
            (fun () ->
                task {
                    let! proofOutcome = prove ()

                    if proofPassed then
                        let fresh =
                            boundedCall
                                "new runnableClass()"
                                (createObj [ "runnableClass" ==> spec.Class ])
                                (callbackBudget scope sanityDeadline)

                        return! executeOwned scope parkState root stack { spec with Instance = fresh }
                    else
                        return proofOutcome
                })

    /// Execute one Callable class. `root` is the shell working directory;
    /// `stack` is the model stack captured at the `js.call(...)` call site so a
    /// check-park carries the same execution marker as a direct park.
    let execute
        (scope: ExecutionScope)
        (parkState: Wanxiangshu.Process.JsSandbox.ParkState option)
        (root: string)
        (runnableClass: obj)
        (stack: string)
        : Task<obj> =
        let notice = scope.OutputNotice()

        let scope =
            { scope with
                OutputNotice = fun () -> notice }

        let validation =
            if scope.Cancellation.IsCancellationRequested then
                Error(failure "CALL_CANCELLED_BEFORE_START" "Host cancellation was already requested")
            elif
                Double.IsNaN scope.HardLimit.TotalMilliseconds
                || Double.IsInfinity scope.HardLimit.TotalMilliseconds
                || scope.HardLimit.TotalMilliseconds <= 0.0
            then
                Error(failure "CALL_HARD_LIMIT_INVALID" "The Host hard limit must be finite and positive")
            else
                validateCallableSpec runnableClass

        match validation with
        | _ when parkState |> Option.exists (fun state -> state.AbandonRequested) ->
            task { return raise (abandoned ()) }
        | Error fail -> Task.FromResult fail
        | Ok spec when
            spec.OutputBudgetBytes < minimumOutputBudget root (scope.OutputNotice())
            || not (ToolResultBound.fitsInWindow (minimumResult root (scope.OutputNotice())))
            ->
            let refused =
                failure "CALL_OUTPUT_BUDGET_TOO_SMALL" "outputBudgetBytes cannot encode the required result"

            refused?minimumOutputBudgetBytes <- float (minimumOutputBudget root (scope.OutputNotice()))
            Task.FromResult refused
        | Ok spec -> executeWithProof scope parkState root stack spec
