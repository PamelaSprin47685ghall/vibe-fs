namespace Wanxiangshu.Foundation

open System.Threading.Tasks

module AsyncSupport =
    val trySetResult: tcs: TaskCompletionSource<'T> -> value: 'T -> bool
    val trySetCanceled: tcs: TaskCompletionSource<'T> -> bool
    val completedTask: unit -> Task
