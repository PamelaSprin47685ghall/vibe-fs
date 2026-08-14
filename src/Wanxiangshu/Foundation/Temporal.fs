namespace Wanxiangshu.Foundation

open System
open System.Threading.Tasks

/// Cancelable one-shot deadline. Cancel leaves Delay permanently pending.
/// Contract only — no Node / setTimeout / Fable JS (G4R-CE S1 / rabbit.md §5.1).
type IDeadlineHandle =
    abstract Delay: Task<unit>
    abstract Cancel: unit -> unit

/// Central delay capability (VERIFY-004): production = Node timer, test = virtual clock.
/// Long budgets (≥1000ms) may unref in physical adapters so a clean process is not held open.
type ITimerPort =
    abstract Delay: milliseconds: int -> IDeadlineHandle
    abstract Dispose: unit -> unit

/// Injectable wall-clock capability — Domain/Application/Session must not call UtcNow directly.
type IClockPort =
    abstract UtcNow: unit -> DateTimeOffset
