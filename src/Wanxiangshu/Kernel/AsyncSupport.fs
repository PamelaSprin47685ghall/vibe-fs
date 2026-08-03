namespace Wanxiangshu.Kernel

open System
open System.Threading.Tasks

/// Fable-compatible TaskCompletionSource helpers. The Fable runtime implementation
/// of TCS does not expose TrySet*/TrySetCanceled, so we use SetResult/SetException
/// with a guard. In .NET this catches the InvalidOperationException thrown on a
/// completed TCS; in Fable the Promise resolver is idempotent and these helpers
/// simply become no-ops after the first call.
module AsyncSupport =

    /// An already-finished `Task`, for a branch with nothing to await.
    ///
    /// NOT `Task.CompletedTask`. That property compiles under .NET and Fable emits
    /// `get_CompletedTask` for it, which `fable-library-js` does not export — so the
    /// module fails to LOAD with `does not provide an export named`. `dotnet build`
    /// cannot see this: the .NET compile is green and the breakage exists only in
    /// the emitted JS.
    ///
    /// A function rather than a value: a module-level `let` becomes a single
    /// module-init promise in Fable, and any later change that made this awaited
    /// once-per-call would then share state across callers.
    let completedTask () : Task = Task.FromResult(()) :> Task

    let trySetResult (tcs: TaskCompletionSource<'T>) (value: 'T) : bool =
        try
            tcs.SetResult(value)
            true
        with _ ->
            false

    let trySetCanceled (tcs: TaskCompletionSource<'T>) : bool =
        try
            tcs.SetException(OperationCanceledException())
            true
        with _ ->
            false
