namespace Wanxiangshu.Execution.Delegation.Fork

open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
module ForkRuntimeBackend =
    val create: clock: IClockPort -> ForkRuntime
