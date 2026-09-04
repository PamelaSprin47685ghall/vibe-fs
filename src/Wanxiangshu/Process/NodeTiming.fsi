namespace Wanxiangshu.Process

open System.Threading.Tasks
open Wanxiangshu.Foundation

module NodeTiming =
    val timerTask: milliseconds: int -> Task<unit>
    val raceExit: exitTask: Task -> milliseconds: int -> Task<bool>
    val nodeTimerPort: unit -> ITimerPort
    val nodeClockPort: unit -> IClockPort
