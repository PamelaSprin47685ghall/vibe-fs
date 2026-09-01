namespace Wanxiangshu.Execution.Session.Wait

open System.Threading.Tasks
open Wanxiangshu.Foundation

module CausalAwait =
    val awaitTask: observer: IWaitObserver -> descriptor: DiagnosticWait -> pending: Task<'T> -> Task<'T>
    val awaitUnit: observer: IWaitObserver -> descriptor: DiagnosticWait -> pending: Task -> Task

    val race:
        observer: IWaitObserver ->
        descriptor: DiagnosticWait ->
        primary: Task<'T> ->
        escape: Task<DiagnosticWaitExit> ->
            Task<Result<'T, DiagnosticWaitExit>>

    val untilSignalOrDeadline:
        observer: IWaitObserver ->
        descriptor: DiagnosticWait ->
        deadline: IDeadlineHandle ->
        tryRead: (unit -> 'T option) ->
        awaitSignal: (unit -> Task<unit>) ->
            Task<Result<'T, DiagnosticWaitExit>>
