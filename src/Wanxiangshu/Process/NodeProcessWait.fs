namespace Wanxiangshu.Process

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.AsyncSupport

module NodeProcessWait =
    type StopCause =
        | Cancellation
        | Deadline

    type WaitOutcome = { StopRequested: StopCause option }

    type private WaitSignal =
        | Closed
        | TimerElapsed
        | Cancelled

    let KillAckGraceMs = 1_000

    let private requestStop (child: NodeProcessHost.ChildProcess) =
        try
            child.Kill()
        with error ->
            child.Failure.Value <- child.Failure.Value |> Option.orElse (Some error.Message)

    let private completeOrAwait (child: NodeProcessHost.ChildProcess) (settled: TaskCompletionSource<WaitSignal>) =
        if child.Exited.Value then
            Task.FromResult Closed
        else
            settled.Task

    let private waitForSignal (child: NodeProcessHost.ChildProcess) milliseconds (ct: CancellationToken) =
        task {
            let settled = TaskCompletionSource<WaitSignal>()
            let onClosed () = trySetResult settled Closed |> ignore
            child.OnExited.Add onClosed

            let timer =
                emitJsExpr (milliseconds, (fun () -> trySetResult settled TimerElapsed |> ignore)) "setTimeout($1, $0)"

            use registration = ct.Register(fun () -> trySetResult settled Cancelled |> ignore)

            try
                return! completeOrAwait child settled
            finally
                emitJsExpr timer "clearTimeout($0)" |> ignore
                child.OnExited.Remove onClosed |> ignore
        }

    let rec private awaitDeadline child deadline ct =
        task {
            let milliseconds = Deadline.nextWaitMs (fun () -> DateTimeOffset.UtcNow) deadline

            if milliseconds = 0 then
                return TimerElapsed
            else
                let! signal = waitForSignal child milliseconds ct
                return! afterSegment child deadline ct signal
        }

    and private afterSegment child deadline ct signal =
        match signal with
        | TimerElapsed when not (Deadline.isExpired (fun () -> DateTimeOffset.UtcNow) deadline) ->
            awaitDeadline child deadline ct
        | other -> Task.FromResult other

    let private stopAtDeadline child ct =
        task {
            requestStop child
            let! signal = waitForSignal child KillAckGraceMs ct
            return { StopRequested = Some(if signal = Cancelled then Cancellation else Deadline) }
        }

    let private awaitStop child deadline ct =
        task {
            match! awaitDeadline child deadline ct with
            | Closed -> return { StopRequested = None }
            | Cancelled ->
                requestStop child
                return { StopRequested = Some Cancellation }
            | TimerElapsed -> return! stopAtDeadline child ct
        }

    let waitForExit (child: NodeProcessHost.ChildProcess) (deadline: Deadline) (ct: CancellationToken) =
        task {
            if child.Exited.Value then
                return { StopRequested = None }
            elif ct.IsCancellationRequested then
                requestStop child
                return { StopRequested = Some Cancellation }
            else
                return! awaitStop child deadline ct
        }
