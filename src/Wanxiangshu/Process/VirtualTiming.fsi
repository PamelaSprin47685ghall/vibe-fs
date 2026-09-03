namespace Wanxiangshu.Process

open System
open Wanxiangshu.Foundation

type VirtualTimerPort =
    { Port: ITimerPort
      Advance: int -> unit
      NowMs: unit -> int }

type VirtualClockPort =
    { Port: IClockPort
      AdvanceMs: int -> unit
      Set: DateTimeOffset -> unit }

module VirtualTiming =
    val createVirtualTimerPort: unit -> VirtualTimerPort
    val createVirtualClockPort: unit -> VirtualClockPort
