namespace Wanxiangshu.Foundation

open System
open System.Threading.Tasks

type IDeadlineHandle =
    abstract Delay: Task<unit>
    abstract Cancel: unit -> unit

type ITimerPort =
    abstract Delay: milliseconds: int -> IDeadlineHandle
    abstract Dispose: unit -> unit

type IClockPort =
    abstract UtcNow: unit -> DateTimeOffset
