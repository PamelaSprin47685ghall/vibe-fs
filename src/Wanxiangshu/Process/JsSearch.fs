namespace Wanxiangshu.Process

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.AsyncSupport
open Wanxiangshu.Host.Contract

module JsSearch =
    let private captureLimit = 1048576L

    let private failure reason =
        createObj [ "ok" ==> false; "code" ==> "RG_FAILED"; "reason" ==> reason ]

    let private fits value =
        LlmFacing.instruction "ok"
        |> LlmFacing.withData (LlmFacing.Data.structuredValue (LlmFacing.Data.ofJs value))
        |> LlmFacing.render
        |> ToolResultBound.fitsInWindow

    let private textOf (capture: ProcessOutput.OutputCollector) : string =
        let chunks = ResizeArray<byte array>()

        match capture.Spool with
        | Some spool -> Spool.readChunksSync spool.Path chunks.Add
        | None -> chunks.AddRange capture.Combined

        let bytes = chunks |> Seq.toArray |> Array.concat
        emitJsExpr bytes "new TextDecoder('utf-8', { fatal: true, ignoreBOM: true }).decode($0)"

    let private parsePaths exitCode (text: string) =
        if text <> "" && not (text.EndsWith("\000")) then
            failure "rg returned an incomplete path list"
        else
            createObj
                [ "ok" ==> true
                  "exitCode" ==> exitCode
                  "paths" ==> text.Split('\000', StringSplitOptions.RemoveEmptyEntries) ]

    let private parseOutput pathsOnly exitCode (text: string) =
        if pathsOnly then
            parsePaths exitCode text
        else
            let messages =
                text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (fun line -> emitJsExpr line "JSON.parse($0)")

            createObj [ "ok" ==> true; "exitCode" ==> exitCode; "messages" ==> messages ]

    let private exitFailure code stderr =
        let detail: string = textOf stderr

        if String.IsNullOrWhiteSpace detail || detail.Length > 512 then
            failure (sprintf "rg exited with code %d" code)
        else
            failure detail

    let private boundedOutput value =
        if fits value then
            value
        else
            failure "rg result exceeds the Host window; narrow the query"

    let private renderOutput pathsOnly (child: NodeProcessHost.ChildProcess) stdout stderr =
        match child.Observe(), child.Failure.Value, child.Rejection.Value with
        | EndEvidence.Confirmed(ProgramEnd.Exited code), None, None when code = 0 || code = 1 ->
            parseOutput pathsOnly code (textOf stdout) |> boundedOutput
        | EndEvidence.Confirmed(ProgramEnd.Exited code), None, None -> exitFailure code stderr
        | ending, fault, rejected -> failure (sprintf "rg end: %A; fault: %A; start: %A" ending fault rejected)

    let private waitClosed (child: NodeProcessHost.ChildProcess) =
        task {
            let closed = TaskCompletionSource<unit>()
            let onClosed () = trySetResult closed () |> ignore
            child.OnExited.Add onClosed

            if child.Exited.Value then
                onClosed ()

            try
                do! closed.Task
            finally
                child.OnExited.Remove onClosed |> ignore
        }

    let private drainAfterStop (child: NodeProcessHost.ChildProcess) (observed: NodeProcessWait.WaitOutcome) =
        task {
            if observed.StopRequested.IsSome && not child.Exited.Value then
                let grace =
                    Deadline.ofBudget ((NodeTiming.nodeClockPort ()).UtcNow()) (TimeSpan.FromMilliseconds 1000.0)

                let! _ = NodeProcessWait.waitForExit child grace CancellationToken.None
                ()
        }

    let private stopReason (scope: JsCallLoop.ExecutionScope) =
        if scope.Cancellation.IsCancellationRequested then
            "rg was cancelled"
        else
            "rg deadline expired"

    let private settleChild
        (scope: JsCallLoop.ExecutionScope)
        pathsOnly
        (observed: NodeProcessWait.WaitOutcome)
        (child: NodeProcessHost.ChildProcess)
        (stdout: ProcessOutput.OutputCollector)
        (stderr: ProcessOutput.OutputCollector)
        =
        task {
            let! authority = scope.ReadOutput()

            return
                match authority with
                | Error reason -> failure reason
                | Ok() when not child.Exited.Value -> failure "rg scope close is unconfirmed; ownership is retained"
                | Ok() when stdout.BytesObserved + stderr.BytesObserved > captureLimit ->
                    failure "rg output exceeds the capture limit; narrow the query"
                | Ok() when observed.StopRequested.IsSome -> failure (stopReason scope)
                | Ok() -> renderOutput pathsOnly child stdout stderr
        }

    let private observeChild
        (scope: JsCallLoop.ExecutionScope)
        pathsOnly
        deadline
        (cancellation: CancellationTokenSource)
        (child: NodeProcessHost.ChildProcess)
        (stdout: ProcessOutput.OutputCollector)
        (stderr: ProcessOutput.OutputCollector)
        complete
        =
        task {
            use registration = cancellation.Token.Register child.Kill

            try
                let! observed = NodeProcessWait.waitForExit child deadline cancellation.Token
                do! drainAfterStop child observed
                let! value = settleChild scope pathsOnly observed child stdout stderr
                complete value
            with error ->
                child.Kill()
                complete (failure error.Message)

            // Unknown close remains owned even after the caller receives a bounded failure.
            do! waitClosed child
        }

    let private observeSpawn scope pathsOnly deadline cancellation stdout stderr complete started =
        match started with
        | Error result ->
            complete (failure (ProcessResult.describe result))
            Task.FromResult()
        | Ok child -> observeChild scope pathsOnly deadline cancellation child stdout stderr complete

    let private runStarted (scope: JsCallLoop.ExecutionScope) root arguments pathsOnly (budget: float) complete =
        task {
            use cancellation = new CancellationTokenSource()
            use parent = scope.Cancellation.Register(fun () -> cancellation.Cancel())
            let stdout = ProcessOutput.createWithOutputLimit 65536L
            let stderr = ProcessOutput.createWithOutputLimit 65536L

            let take capture add bytes =
                add capture bytes

                if stdout.BytesObserved + stderr.BytesObserved > captureLimit then
                    cancellation.Cancel()

            let request =
                { FileName = "rg"
                  Arguments = Array.toList arguments
                  WorkingDirectory = Some root
                  Environment = None
                  Stdin = None }

            let context =
                { WorkingDirectory = Some root
                  HardLimit = TimeSpan.FromMilliseconds budget }

            let deadline =
                Deadline.ofBudget ((NodeTiming.nodeClockPort ()).UtcNow()) context.HardLimit

            try
                let! started =
                    NodeProcessHost.spawn
                        request
                        context
                        (take stdout ProcessOutput.addStdout)
                        (take stderr ProcessOutput.addStderr)
                        cancellation.Token

                do! observeSpawn scope pathsOnly deadline cancellation stdout stderr complete started
            finally
                ProcessOutput.discard stdout
                ProcessOutput.discard stderr
        }

    let private runOwned (scope: JsCallLoop.ExecutionScope) root arguments pathsOnly complete =
        task {
            let! authority = scope.ReadOutput()
            let! remaining = scope.RemainingMs()
            let budget = min (float remaining) scope.HardLimit.TotalMilliseconds

            match authority with
            | Error reason -> complete (failure reason)
            | Ok() when scope.Cancellation.IsCancellationRequested -> complete (failure "rg was cancelled before start")
            | Ok() when Double.IsNaN budget || Double.IsInfinity budget || budget <= 0.0 ->
                complete (failure "rg deadline expired before start")
            | Ok() -> do! runStarted scope root arguments pathsOnly budget complete
        }

    let execute (scope: JsCallLoop.ExecutionScope) root arguments pathsOnly =
        let completed = TaskCompletionSource<obj>()
        let complete value = trySetResult completed value |> ignore

        let start () =
            task {
                try
                    do! runOwned scope root arguments pathsOnly complete
                with error ->
                    complete (failure error.Message)
            }
            :> Task

        if scope.Cancellation.IsCancellationRequested then
            Task.FromResult(failure "rg was cancelled before start")
        elif not (fits (createObj [ "ok" ==> true; "exitCode" ==> 0; "messages" ==> [||] ])) then
            Task.FromResult(failure "Host window cannot encode an rg result")
        elif scope.Own start then
            completed.Task
        else
            Task.FromResult(failure "process owner is closing")
