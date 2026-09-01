namespace Wanxiangshu.Process

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation

type VirtualTimerPort =
    { Port: ITimerPort
      Advance: int -> unit
      NowMs: unit -> int }

type VirtualClockPort =
    { Port: IClockPort
      AdvanceMs: int -> unit
      Set: DateTimeOffset -> unit }

module PtyTiming =
    val timerTask: milliseconds: int -> Task<unit>
    val raceExit: exitTask: Task -> milliseconds: int -> Task<bool>
    val nodeTimerPort: unit -> ITimerPort
    val nodeClockPort: unit -> IClockPort
    val createVirtualTimerPort: unit -> VirtualTimerPort
    val createVirtualClockPort: unit -> VirtualClockPort
