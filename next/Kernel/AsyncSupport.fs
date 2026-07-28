namespace Wanxiangshu.Next.Kernel

open System
open System.Threading.Tasks

/// Fable-compatible TaskCompletionSource helpers. The Fable runtime implementation
/// of TCS does not expose TrySet*/TrySetCanceled, so we use SetResult/SetException
/// with a guard. In .NET this catches the InvalidOperationException thrown on a
/// completed TCS; in Fable the Promise resolver is idempotent and these helpers
/// simply become no-ops after the first call.
module AsyncSupport =

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
