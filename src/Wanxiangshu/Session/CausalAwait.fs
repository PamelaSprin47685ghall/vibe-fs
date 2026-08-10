namespace Wanxiangshu.Session

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel

/// Bracket helpers that register a diagnostic wait lease around a real Task.
/// Business order is unchanged: CE suspend → observe → Task settle → dispose.
module CausalAwait =

    let private classifyExn (exn: exn) : DiagnosticWaitExit =
        match exn with
        | :? OperationCanceledException -> DiagnosticWaitExit.WaitCancelled
        | :? TimeoutException -> DiagnosticWaitExit.WaitTimedOut
        | _ when exn.Message.Contains("cancel") || exn.Message.Contains("Cancel") -> DiagnosticWaitExit.WaitCancelled
        | _ when exn.Message.Contains("timed out") || exn.Message.Contains("Timeout") -> DiagnosticWaitExit.WaitTimedOut
        | _ -> DiagnosticWaitExit.WaitFailed

    let awaitTask (observer: IWaitObserver) (descriptor: DiagnosticWait) (pending: Task<'T>) : Task<'T> =
        task {
            use lease = observer.Enter descriptor

            try
                let! value = pending
                lease.MarkExit DiagnosticWaitExit.WaitResolved
                return value
            with ex ->
                lease.MarkExit(classifyExn ex)
                return raise ex
        }

    let awaitUnit (observer: IWaitObserver) (descriptor: DiagnosticWait) (pending: Task) : Task =
        task {
            use lease = observer.Enter descriptor

            try
                do! pending
                lease.MarkExit DiagnosticWaitExit.WaitResolved
            with ex ->
                lease.MarkExit(classifyExn ex)
                return raise ex
        }
        :> Task

    /// Race primary against a pre-built escape loser task. Shown as one composite wait.
    /// Fable Task has no WhenAny — use Promise.race like FinalityController / JoinTool.
    let race
        (observer: IWaitObserver)
        (descriptor: DiagnosticWait)
        (primary: Task<'T>)
        (escape: Task<DiagnosticWaitExit>)
        : Task<Result<'T, DiagnosticWaitExit>> =
        task {
            use lease = observer.Enter descriptor

            let taggedPrimary: Task<obj> =
                task {
                    let! value = primary
                    return box (Choice1Of2 value)
                }

            let taggedEscape: Task<obj> =
                task {
                    let! exit = escape
                    return box (Choice2Of2 exit)
                }

            let! winnerObj = emitJsExpr (taggedPrimary, taggedEscape) "Promise.race([$0, $1])": Task<obj>

            match unbox<Choice<'T, DiagnosticWaitExit>> winnerObj with
            | Choice1Of2 value ->
                lease.MarkExit DiagnosticWaitExit.WaitResolved
                return Ok value
            | Choice2Of2 exit ->
                lease.MarkExit exit
                return Error exit
        }
