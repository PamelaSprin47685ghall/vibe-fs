namespace Wanxiangshu.Process

open System.Threading.Tasks

/// Host-owned execution loop for `js.run` (PLAN phase 2). The model writes one
/// Runnable class; this module owns the loop and returns one structured
/// outcome or a stable failure object.
module JsRunLoop =
    /// Execute one Runnable class against `root` as the shell working
    /// directory. `stack` is the model stack of the `js.run(...)` call site,
    /// used as the execution marker when a check verdict parks the run.
    val execute:
        parkState: JsSandbox.ParkState option -> root: string -> runnableClass: obj -> stack: string -> Task<obj>
