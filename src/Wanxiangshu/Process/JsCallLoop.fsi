namespace Wanxiangshu.Process

open System
open System.Threading
open System.Threading.Tasks

/// Host-owned execution loop for `js.call`. The model writes one
/// Callable class; this module owns the loop and returns one structured
/// outcome or a stable failure object.
module JsCallLoop =
    type ExecutionScope =
        { Cancellation: CancellationToken
          HardLimit: TimeSpan
          SynchronousBudgetMs: int
          OutputNotice: unit -> string
          ReadOutput: unit -> Task<Result<unit, string>>
          BorrowDeadline: int64 -> Task<(unit -> unit)>
          Own: (unit -> Task) -> bool }

    val standalone: ExecutionScope

    /// Execute one Callable class against `root` as the shell working
    /// directory. `stack` is the model stack of the `js.call(...)` call site,
    /// used as the execution marker when a check verdict parks the run.
    val execute:
        scope: ExecutionScope ->
        parkState: JsSandbox.ParkState option ->
        root: string ->
        runnableClass: obj ->
        stack: string ->
            Task<obj>
